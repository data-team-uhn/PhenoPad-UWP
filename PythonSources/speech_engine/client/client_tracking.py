import sys, os
PARENT_DIRECTORY = os.path.dirname(os.path.abspath(__file__))
CLIENT_DIRECTORY = os.path.join(PARENT_DIRECTORY, '..', 'client')
SERVER_DIRECTORY = os.path.join(PARENT_DIRECTORY, '..', 'server')
CONFIG_DIRECTORY = os.path.join(PARENT_DIRECTORY, '..', 'configs')
SCRIPT_DIRECTORY = os.path.join(PARENT_DIRECTORY, '..', 'scripts')
sys.path.append(CLIENT_DIRECTORY)
sys.path.append(SERVER_DIRECTORY)

from ws4py.client.threadedclient import WebSocketClient
import logging
import argparse
import json
import urllib

from common import getuuid


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
        



def main():

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

    args.uri = args.uri + '?%s' % (urllib.urlencode([("manager_id", args.manager)]))

    try:
        tracking_client = TrackingClient(args.uri)
        tracking_client.connect()

        tracking_client.run_forever()
    except KeyboardInterrupt:
        tracking_client.close()



if __name__ == '__main__':
    main()

