
from ws4py.client.threadedclient import WebSocketClient

import logging
import argparse
import time
import threading
import sys
import urllib
import Queue
import json
import time
import os


CLEAN_BUFFER_FREQ = 1        # clean buffer 100 times per seconds (apparently PhenoPad sends 640 bytes each time)

def rate_limited(maxPerSecond):
    minInterval = 1.0 / float(maxPerSecond)
    def decorate(func):
        lastTimeCalled = [0.0]
        def rate_limited_function(*args,**kargs):
            elapsed = time.clock() - lastTimeCalled[0]
            leftToWait = minInterval - elapsed
            if leftToWait>0:
                time.sleep(leftToWait)
            ret = func(*args,**kargs)
            lastTimeCalled[0] = time.clock()
            return ret
        return rate_limited_function
    return decorate


class MyClient(WebSocketClient):

    def __init__(self, audiofile, url, protocols=None, extensions=None, heartbeat_freq=None, byterate=32000,
                 save_adaptation_state_filename=None, send_adaptation_state_filename=None):
        super(MyClient, self).__init__(url, protocols, extensions, heartbeat_freq)
        self.final_hyps = []
        self.audiofile = audiofile
        self.byterate = byterate
        self.final_hyp_queue = Queue.Queue()
        self.save_adaptation_state_filename = save_adaptation_state_filename
        self.send_adaptation_state_filename = send_adaptation_state_filename
        self.counter = 0

    @rate_limited(CLEAN_BUFFER_FREQ)
    def send_data(self, data):
        self.send(data, binary=True)


    def opened(self):
        print "Socket opened!"
        def send_data_to_ws():
            if self.send_adaptation_state_filename is not None:
                logging.info("Sending adaptation state from %s" % self.send_adaptation_state_filename)
                try:
                    adaptation_state_props = json.load(open(self.send_adaptation_state_filename, "r"))
                    self.send(json.dumps(dict(adaptation_state=adaptation_state_props)))
                except:
                    e = sys.exc_info()[0]
                    logging.error("Failed to send adaptation state: %s" % str(e))
            
            with self.audiofile as audiostream:
                for block in iter(lambda: audiostream.read(self.byterate/CLEAN_BUFFER_FREQ), ""):
                    self.counter += 1
                    self.send_data(block)

                logging.info('--- Sending useless stuff ---')
                while True:
                    try:
                        #self.send_data(b'\xAA' * (self.byterate/4))
                        self.send_data(b'\x00' * (self.byterate/4))
                    except:
                        logging.error('Failed to send empty data due to %s' % e)

            logging.info("Audio sent, now sending EOS")
            self.send("EOS")

        t = threading.Thread(target=send_data_to_ws)
        t.start()


    def received_message(self, m):
        response = json.loads(str(m))
        #print >> sys.stderr, "RESPONSE:", response
        #print >> sys.stderr, "JSON was:", m
        if response['status'] == 0:
            if 'result' in response:
                trans = response['result']['hypotheses'][0]['transcript']
                if response['result']['final']:
                    print >> sys.stderr, trans,
                    self.final_hyps.append(trans)
                    print >> sys.stderr, '\r%s' % trans.replace("\n", "\\n")

                    #print(response['result']['hypotheses'][0]['word-alignment'])

                else:
                    print_trans = trans.replace("\n", "\\n")
                    if len(print_trans) > 80:
                        print_trans = "... %s" % print_trans[-76:]
                    #print >> sys.stderr, '\r%s' % print_trans,
            if 'adaptation_state' in response:
                if self.save_adaptation_state_filename:
                    logging.info("Saving adaptation state to %s" % self.save_adaptation_state_filename)
                    with open(self.save_adaptation_state_filename, "w") as f:
                        f.write(json.dumps(response['adaptation_state']))
        else:
            logging.error("Received error from server (status %d)" % response['status'])
            if 'message' in response:
                logging.error("Error message: %s" % str(response['message']))


    def get_full_hyp(self, timeout=60):
        return self.final_hyp_queue.get(timeout)

    def closed(self, code, reason=None):
        #print "Websocket closed() called"
        #print >> sys.stderr
        self.final_hyp_queue.put(" ".join(self.final_hyps))


def main():

    logging.basicConfig(level=logging.INFO, format="%(levelname)8s %(asctime)s %(message)s ")
    logging.info('Starting up audio client')

    parser = argparse.ArgumentParser(description='Command line client for kaldigstserver')
    parser.add_argument('-i', '--ip', default="localhost", dest="ip", help="Server websocket URI")
    #parser.add_argument('-u', '--uri', default="ws://localhost:8888/audio", dest="uri", help="Server websocket URI")
    parser.add_argument('-r', '--rate', default=16000, dest="rate", type=int, help="Sampling rate")
    parser.add_argument('-f', '--format', default='F32LE', help="Format. S16LE or F32LE")
    parser.add_argument('--save-adaptation-state', help="Save adaptation state to file")
    parser.add_argument('--send-adaptation-state', help="Send adaptation state from file")
    parser.add_argument('-m', '--manager', dest="manager", 
                    help="Client manager PID", required=True)
    #parser.add_argument('--content-type', default='', help="Use the specified content type (empty by default, for raw files the default is  audio/x-raw, layout=(string)interleaved, rate=(int)<rate>, format=(string)S16LE, channels=(int)1")
    parser.add_argument('audiofile', help="Audio file to be sent to the server", type=argparse.FileType('rb'), default=sys.stdin)
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
    uri = uri + '?%s' % (urllib.urlencode([("content-type", content_type), ("manager_id", args.manager)]))

    ws = MyClient(args.audiofile, uri, byterate=byterate,
                  save_adaptation_state_filename=args.save_adaptation_state, send_adaptation_state_filename=args.send_adaptation_state)
    ws.connect()
    
    result = ws.get_full_hyp()
    print result.encode('utf-8')

if __name__ == "__main__":
    main()

