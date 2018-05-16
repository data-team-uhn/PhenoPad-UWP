######################################
# # This is a websocket server commonicating with ODAS and ReSpeaker #
######################################
import socket
import time
import json 
import math
import sys
import numpy as np
from datetime import datetime
import threading
from multiprocessing.pool import ThreadPool
from multiprocessing import Process, Queue
from abc import ABCMeta, abstractmethod
#import micarray_diarization_engine as mde
import os
import tempfile
import copy

#from entity.Track import Track

import logging
logger = logging.getLogger(__name__)
logger.propagate = False
logging.basicConfig(filename='audio_server.log', level=logging.DEBUG, format="%(levelname)8s %(asctime)s %(message)s ")
logging.debug('Starting up microphone array server')



import numpy as np
#import sklearn.preprocessing
import math

class Track:    
    def __init__(self):        
        self.id = -1        
        self.start = -1
        self.end = -1 
        self.state = 0 
        self.np_vectors = np.empty((0, 2)) 
        self.vectors = []
        self.cache_num = 20
        self.mean_vector = None
        self.mean_angle = None
        self.speaker = -1

    def append_vector(self, x, y):
        self.vectors.append([x, y])
           
    def get_mean_vector(self):
        if not self.mean_vector is None:
            return self.mean_vector
        if len(self.vectors) > self.cache_num:
            self.np_vectors = np.array(self.vectors)
            #norm_vector = sklearn.preprocessing.normalize(self.np_vectors[:self.cache_num, ])
            #self.mean_vector = np.mean(norm_vector, axis=0)
            #return self.mean_vector
            return self.np_vectors
        return None

    def get_angle(self):
        if not self.mean_angle is None:
            return self.mean_angle
        if not self.mean_vector is None:
            self.mean_angle = math.degrees(math.atan2(self.mean_vector[1], self.mean_vector[0]))
            return self.mean_angle
        return None


class MyWebsocketServer(metaclass=ABCMeta):
    def __init__(self, port, buffer_size=100):
        self.port = port
        self.buffer_size = buffer_size
        self.conn = None

    def start_server(self):
        # Set up socket
        with socket.socket(socket.AF_INET, socket.SOCK_STREAM) as sock:
            # Allow re-binding the same port
            sock.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
            # Bind to port on any interface
            sock.bind(('0.0.0.0', self.port))
            sock.listen(1) # allow backlog of 1

            print(socket.gethostbyname_ex(socket.gethostname()))

            print("BEGIN LISTENING ON PORT", self.port)
            # Begin listening for connections
            while(True):
                conn, addr = sock.accept()
                print("\nCONNECTION:", addr)
                self.conn = conn
                return 1
                #return self.loop_func(conn)
                #return conn

    @abstractmethod
    def loop_func(self):
        pass



class TrackWebsocketServer(MyWebsocketServer):
    def __init__(self, port, track_queue, buffer_size=100, num_track=4, num_speaker=2):
        super().__init__(port, buffer_size)
        self.num_track = num_track
        self.num_speaker = num_speaker
        self.ang_cache_num = 20
        self.ang_dif_thred = 30 # two tracks whose angle difference is less than ang_dif_thred should be the same speaker
        self.track_queue = track_queue
        self.TIMESTAMP_LENGTH = 0.008 # 8 ms every timestamp, TODO make it clear

    def loop_func(self):
        if self.conn == None:
            return
        conn = self.conn

        # track 4 speakers at most
        # id, angle, start timestamp, end timestamp
        track_info = []
        track_mean_notknown = []
        for i in range(self.num_track):
            track_info.append(Track())

        #
        trackinfo_log = open("track_info.log", 'w')

        # Receive and handle command
        remainingTrack = ""
        while(True):
            data = conn.recv(self.buffer_size)
            if len(data) != 0:
                stream = remainingTrack + data.decode('ascii')
                strs = stream.split("}\n{")
                if len(strs) < 2:
                    remainingTrack = stream
                    continue
                for i in range(len(strs)):
                    if i == len(strs) - 1:
                        remainingTrack = strs[i]
                        continue
                    #TODO more efficient way
                    if strs[i][0] != '{':
                        strs[i] = '{' + strs[i]
                    if strs[i][-2] != '}':
                        strs[i] =  strs[i] + "}"
                    #print(strs[i] + "\n")
                    track = json.loads(strs[i])
                    timestamp = int(track['timeStamp'])
                    #print(strs[i])
                    trackinfo_log.write(strs[i] + '\n')

                    for i in range(self.num_track): # len(track['src']) should equal to self.num_track!
                        src = track['src'][i]
                        tid = src['id']
                        #if src['tag'] == 'dynamic':
                        x = float(src['x'])
                        y = float(src['y'])

                        '''
                        angle = math.degrees(math.atan2(y, x))
                        if x*x + y*y > 0.5*0.5:
                            print(str(src['id']) + "\t" + str(angle) + "\t" + str(math.sqrt(x*x+y*y)) + "\t" + str(src['activity']))
                        else:
                            print("0\t0.0\t0.0")
                        '''
                        if tid == 0 and track_info[i].id != -1:
                            # track i stops
                            track_info[i].end = track_info[i].end * self.TIMESTAMP_LENGTH
                            track_info[i].state = 2
                            track_info[i].append_vector(x, y)
                            self.track_queue.put(copy.copy([track_info[i]]))

                            track_info[i] = Track()
                            continue

                        if tid != 0 and track_info[i].id != -1 and track_info[i].id != tid:
                            # track i stops and change to a new track
                            logger.warning('this should not happen, track id should change to 0 before to another track')
                            continue
                            
                        
                        if tid != 0 and x*x+y*y>0.5*0.5:
                            if track_info[i].id == -1:
                                # new track id
                                track_info[i].id = tid
                                track_info[i].start = timestamp * self.TIMESTAMP_LENGTH 
                                track_info[i].end = timestamp
                                track_info[i].append_vector(x, y)
                                self.track_queue.put(copy.copy([track_info[i]]))
                                print(copy.copy([track_info[i]]))
                                track_mean_notknown.append(tid)

                            elif track_info[i].id == tid:
                                # existing track id
                                track_info[i].append_vector(x, y)
                                track_info[i].end = timestamp # keep tracking the last timestamp
                                if tid in track_mean_notknown:
                                    if not track_info[i].get_mean_vector() is None:
                                        track_info[i].state = 1
                                        self.track_queue.put([copy.copy(track_info[i])])
                                        track_mean_notknown.remove(tid)

                            else:
                                # again, this should not happen
                                logger.warning('this should not happen, track id should change to 0 before to another track')
                                continue
            else:
                trackinfo_log.close()
                print("Shutting down connection")
                conn.shutdown(socket.SHUT_RDWR)
                conn.close()
                return 0

    def mean_of_angles(self, angles):
        if len(angles) == 0:
            return 1000
        if type(angles) == list:
            angles = np.array(angles)
        if np.min(angles) < 0 and \
                np.max(angles) > 0 and \
                np.max(angles) - np.min(angles) > 180: # special dealing if contains both neg and pos angles
            mean_neg = np.mean(angles[angles < 0])
            mean_posi = np.mean(angles[angles > 0])
            mean_ang = mean_posi + (360 - mean_posi + mean_neg) / 2 \
                    if mean_posi - mean_neg >= 180 else mean_posi - (mean_posi - mean_neg) / 2
            if mean_ang > 180:
                mean_ang = mean_ang - 360
            return mean_ang
        else:
            return np.mean(angles)


class AudioWebsocketServer(MyWebsocketServer):
    def __init__(self, port, audiofile=None, buffer_size=100, num_channel=4, sample_rate=16000, bit_number=32):
        super().__init__(port, buffer_size)
        self.num_channel = num_channel
        self.bit_number = bit_number
        self.buffer_size = num_channel * sample_rate * (bit_number // 8)
        #self.audio_queue = Queue()
        self.audiofile =  'tmp/'+next(tempfile._get_candidate_names()) +  time.strftime("_%Y%M%d_%H_%M_%S_") + '.raw'
        if audiofile:
            self.audiofile = audiofile

    def get_audio_file_name(self):
        return self.audiofile

    def loop_func(self):
        if self.conn == None:
            return
        conn = self.conn
        '''
        rawFiles = []
        for i in range(nChannels):
            rawFiles.append(open("channel_"+str(i+1)+".raw", "wb"))
        '''
        start_timestamp = time.time()
        rawfile = open(self.audiofile, 'wb')
        # Receive and handle command
        while(True):
            time.sleep(1)
            data = conn.recv(self.buffer_size)
            #print(len(data))
            if len(data) > 0:
                rawfile.write(data)
                #self.audio_queue.put(data, True) # save data, with block
                '''
                offset = 0
                jump = self.bit_number // 8
                delta = jump - 1
                while offset < len(data):
                    for i in range(self.num_channel):
                        begin = offset+i*jump
                        rawFiles[i].write(data[begin:begin+delta+1])
                    offset += jump * self.num_channel
                '''
            else:
                print("Shutting down conncection")
                '''
                for rf in rawFiles:
                    rf.close()
                '''
                rawfile.close()
                conn.shutdown(socket.SHUT_RDWR)
                conn.close()
                return 0

class DiarizationManager:
    def __init__(self):
        self.track_queue = Queue()
        self.audio_queue = Queue()
        self.track_server = TrackWebsocketServer(port=9000, track_queue=self.track_queue)
        self.audio_server = AudioWebsocketServer(port=10000)
        #self.diarization_status_queue = Queue()
        #self.diarization_result_queue = Queue()
        #self.diarization_process = None
        self.track_process = None
        self.audio_process = None
        self.status_queue = Queue()

    def start_diarization(self):
        pool = ThreadPool(processes=2)
        track_conn_result = pool.apply_async(self.track_server.start_server, args=())
        audio_conn_result = pool.apply_async(self.audio_server.start_server, args=())

        
        track_res = track_conn_result.get()
        audio_res = audio_conn_result.get()
        if track_res == 1 and audio_res == 1:
            logger.debug("Successfully build mic array websocket connection")
            self.track_process = Process(target=self.track_server.loop_func, args=())
            self.audio_process = Process(target=self.audio_server.loop_func, args=())
            self.track_process.start()
            self.audio_process.start()

            '''
            logger.debug("Starting diarization process")
            self.my_pid = os.getpid()
            self.diarization_process = \
                Process(target=mde.start_micarray_diarization_engine, \
                args=(self.track_queue, self.audio_server.get_audio_file_name(), self.diarization_result_queue,\
                self.status_queue, self.my_pid))
            self.diarization_process.start()

            while True:
                try:
                    result = self.diarization_result_queue.get(False)
                    print("Results: " + str(result))
                except:
                    time.sleep(1)
                    logger.info("waiting 1 second...")
            ''' 

            """
            while True:
                time.sleep(1)
                print('###    ' + str(self.track_queue.qsize()))
            """
        


    def lookat_diarization_result(self):
        while(true):
            time.sleep(1)
            result = self.diarization_result_queue.get(False)
            print(result)


if __name__ == "__main__":
    dm = DiarizationManager()
    dm.start_diarization()



