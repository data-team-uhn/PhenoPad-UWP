import sys, os
PARENT_DIRECTORY = os.path.dirname(os.path.abspath(__file__))
CLIENT_DIRECTORY = os.path.join(PARENT_DIRECTORY, '..', 'client')
SERVER_DIRECTORY = os.path.join(PARENT_DIRECTORY, '..', 'server')
CONFIG_DIRECTORY = os.path.join(PARENT_DIRECTORY, '..', 'configs')
SCRIPT_DIRECTORY = os.path.join(PARENT_DIRECTORY, '..', 'scripts')
sys.path.append(CLIENT_DIRECTORY)
sys.path.append(SERVER_DIRECTORY)

import argparse
import sys
import urllib

import socket
import time
import json 
import math
import numpy as np
from datetime import datetime
import threading
from abc import ABCMeta, abstractmethod
import os
import tempfile
import copy
import traceback

import logging

from ws4py.client.threadedclient import WebSocketClient

from odas_audio_control import MyWebsocketServer

from common import getuuid

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


class TrackingClient(WebSocketClient):

    def __init__(self, uri):
        self.uri = uri
        self.local_id = getuuid()
        self.manager_id = None
        self.handler_id = None
        #WebSocketClient.__init__(self, url=uri, heartbeat_freq=10)
        WebSocketClient.__init__(self, url=uri)


    def prompt(self):
        return 'TrackingClient(%s, %s, %s) ' % (str(self.local_id), str(self.handler_id), str(self.manager_id))


    def opened(self):
        pass


    def closed(self, code, reason=None):
        logging.info(self.prompt() + ' has closed down ' + str(code) + str(reason))


    def received_message(self, m):
        logging.info(self.prompt() + ' => msg length=%d ' % (len(m)))
        



class TrackWebsocketServer(MyWebsocketServer):
    def __init__(self, port, track_queue=None, buffer_size=100, num_track=4, num_speaker=2, track_client=None):
        super().__init__(port, buffer_size)
        self.num_track = num_track
        self.num_speaker = num_speaker
        self.ang_cache_num = 20
        self.ang_dif_thred = 30 # two tracks whose angle difference is less than ang_dif_thred should be the same speaker
        #self.track_queue = track_queue
        self.TIMESTAMP_LENGTH = 0.008 # 8 ms every timestamp, TODO make it clear
        self.track_client = track_client


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
        trackinfo_log = open("/tmp/track_info.log", 'w')

        # Receive and handle command
        remainingTrack = ""
        while(True):
            #logging.info('temp')
            data = conn.recv(self.buffer_size)
            logging.info('Data length %d' % len(data))
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
                    logging.info(strs[i])
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
                            #self.track_queue.put(copy.copy([track_info[i]]))
                            self.track_client.send(json.dumps(copy.copy([track_info[i]])))
                            logging.info(str(copy.copy([track_info[i]])))
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
                                logging.info(str(copy.copy([track_info[i]])))
                                track_mean_notknown.append(tid)

                            elif track_info[i].id == tid:
                                # existing track id
                                track_info[i].append_vector(x, y)
                                track_info[i].end = timestamp # keep tracking the last timestamp
                                if tid in track_mean_notknown:
                                    if not track_info[i].get_mean_vector() is None:
                                        track_info[i].state = 1
                                        #self.track_queue.put([copy.copy(track_info[i])])
                                        self.track_client.send(json.dumps(copy.copy([track_info[i]])))
                                        logging.info(str(copy.copy([track_info[i]])))
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


if __name__ == '__main__':
    logging.basicConfig(level=logging.INFO, format="%(levelname)8s %(asctime)s %(message)s ")
    logging.info('Starting up Tracking client')
    parser = argparse.ArgumentParser(description='Stub client for ODAS tracking')
    parser.add_argument('-u', '--uri', default="ws://localhost:8888/tracking", \
                    dest="uri", help="Server<-->Tracking websocket URI")
    parser.add_argument('-m', '--manager', dest="manager", 
                    help="Client manager PID", required=True)
    parser.add_argument('-p', '--ip', default=None, \
                    dest="ip", help="Server IP address")                                        
    args = parser.parse_args()

    if args.ip is not None:
        args.uri = 'ws://' + str(args.ip.strip()) + ':8888/tracking'

    #args.uri = args.uri + '?%s' % (urllib.urlencode([("manager_id", args.manager)]))
    args.uri = args.uri + '?%s' % (urllib.parse.urlencode([("manager_id", args.manager)]))

    try:
        tracking_client = TrackingClient(args.uri)
        tracking_client.connect()
        #thread.start_new_thread(tracking_client.run_forever)
        threading.Thread(target=tracking_client.run_forever).start()

        track_server = TrackWebsocketServer(port=9000, track_client=tracking_client)
        track_server.start_server()

        track_server.loop_func()
    except KeyboardInterrupt:
        tracking_client.close()
        traceback.print_exc()


            