######################################
# # This is a websocket server commonicating with ODAS and ReSpeaker #
######################################
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
import array
import struct

from ws4py.client.threadedclient import WebSocketClient

import logging

from common import getuuid

BYTES_PER_FRAME = 2     # Number of bytes in each audio frame. Int 16
CHANNELS = 1            # Number of channels.
SAMPLING_RATE = 16000   # Audio sampling rate.


class MyWebsocketServer(metaclass=ABCMeta):
#class MyWebsocketServer():
    #__metaclass__ = ABCMeta

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
            logging.info("Trying to listen on port " + str(self.port))
            sock.bind(('0.0.0.0', self.port))
            sock.listen(1) # allow backlog of 1

            print(socket.gethostbyname_ex(socket.gethostname()))

            logging.info("BEGIN LISTENING ON PORT " + str(self.port))
            # Begin listening for connections
            while(True):
                conn, addr = sock.accept()
                logging.info("\nCONNECTION: " + str(addr))
                self.conn = conn
                return 1
                #return self.loop_func(conn)
                #return conn

    @abstractmethod
    def loop_func(self):
        pass




class MyClient(WebSocketClient):

    def __init__(self, url, protocols=None, extensions=None, heartbeat_freq=None, byterate=32000,
                 save_adaptation_state_filename=None, send_adaptation_state_filename=None):
        super(MyClient, self).__init__(url, protocols, extensions, heartbeat_freq)
        self.byterate = byterate
        self.save_adaptation_state_filename = save_adaptation_state_filename
        self.send_adaptation_state_filename = send_adaptation_state_filename
        self.counter = 0

        self.uri = uri
        self.local_id = getuuid()
        self.manager_id = None
        self.handler_id = None

    def prompt(self):
        return 'AudioClient(%s, %s, %s) ' % (str(self.local_id), str(self.handler_id), str(self.manager_id))

    # Wrapper to not have to write binary = True    
    def send_data(self, data):
        self.send(data, binary=True)


    def opened(self):
        logging.info(self.prompt() + ' connection opened')
        

    def received_message(self, m):
        # This client should not receive any message?
        logging.info(self.prompt() + str(m))


    def closed(self, code, reason=None):
        logging.info(self.prompt() + ' connection closed')



class AudioWebsocketServer(MyWebsocketServer):
    def __init__(self, port, audiofile=None, buffer_size=100, num_channel=4, sample_rate=16000, bit_number=32, audio_client=None):
        super().__init__(port, buffer_size)
        self.num_channel = num_channel
        self.bit_number = bit_number
        self.buffer_size = num_channel * sample_rate * (bit_number // 8)
        #self.audio_queue = Queue()
        self.audiofile =  '/tmp/'+next(tempfile._get_candidate_names()) +  time.strftime("_%Y%M%d_%H_%M_%S_") + '.raw'
        if audiofile:
            self.audiofile = audiofile
        self.audio_client = audio_client


    def get_audio_file_name(self):
        return self.audiofile


    def average_channels_to_one(self, n_channel,  all_bytes):
        if len(all_bytes) % (BYTES_PER_FRAME * n_channel) != 0:
            logger.warning("Need handling remaining bytes")
        audio_ints = array.array('h', all_bytes) # 16 bit PCM
        np_audio_ints = np.array(audio_ints, dtype=np.int16)
        merged_ints = np.mean(np_audio_ints.reshape(-1, n_channel), axis=-1).astype(np.int16) # take average every n_channel samples
        merged_ints = merged_ints.tolist()

        # pack and return, TODO how to average bytes directly
        return struct.pack('<%dh'%len(merged_ints), *merged_ints)


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

                merged = self.average_channels_to_one(self.num_channel, data)
                self.audio_client.send_data(merged)
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


if __name__ == '__main__':
    logging.basicConfig(level=logging.DEBUG, format="%(levelname)8s %(asctime)s %(message)s ")
    logging.debug('Starting up microphone array server (AUDIO)')

    parser = argparse.ArgumentParser(description='Command line client for kaldigstserver')
    parser.add_argument('-i', '--ip', default="localhost", dest="ip", help="Server websocket URI")
    #parser.add_argument('-u', '--uri', default="ws://localhost:8888/audio", dest="uri", help="Server websocket URI")
    parser.add_argument('-r', '--rate', default=16000, dest="rate", type=int, help="Sampling rate")
    parser.add_argument('-f', '--format', default='S16LE', help="Format. S16LE or F32LE")
    parser.add_argument('--save-adaptation-state', help="Save adaptation state to file")
    parser.add_argument('--send-adaptation-state', help="Send adaptation state from file")
    parser.add_argument('-m', '--manager', dest="manager", 
                    help="Client manager PID", required=True)
    #parser.add_argument('--content-type', default='', help="Use the specified content type (empty by default, for raw files the default is  audio/x-raw, layout=(string)interleaved, rate=(int)<rate>, format=(string)S16LE, channels=(int)1")
    #parser.add_argument('audiofile', help="Audio file to be sent to the server", type=argparse.FileType('rb'), default=sys.stdin)
    args = parser.parse_args()

    content_type = ''
    byterate = 0
    if args.format == 'S16LE':
        content_type = "audio/x-raw, layout=(string)interleaved, rate=(int)%d, format=(string)S16LE, channels=(int)1" %(args.rate)
        byterate = args.rate * 2
    
    if args.format == 'F32LE':
        content_type = "audio/x-raw, layout=(string)interleaved, rate=(int)%d, format=(string)F32LE, channels=(int)1" %(args.rate)
        byterate = args.rate * 4

    uri = "ws://" + args.ip + ":8888/audio"
    #uri = uri + '?%s' % (urllib.urlencode([("content-type", content_type), ("manager_id", args.manager)]))
    uri = uri + '?%s' % (urllib.parse.urlencode([("content-type", content_type), ("manager_id", args.manager)]))

    try:
        ws = MyClient(uri, byterate=byterate,
                    save_adaptation_state_filename=args.save_adaptation_state, send_adaptation_state_filename=args.send_adaptation_state)
        ws.connect()
        ##thread.start_new_thread(ws.run_forever)
        threading.Thread(target=ws.run_forever).start()

        # Start "server" to receive from ODAS, also act as client
        audio_server = AudioWebsocketServer(port=10000, audio_client=ws)
        audio_server.start_server()

        audio_server.loop_func()
    except:
        logging.info('Experienced issues with audio?')
        ws.close()
        traceback.print_exc()

        