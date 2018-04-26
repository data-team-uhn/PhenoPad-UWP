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
import urllib

from common import getuuid


class DiarizationClient(WebSocketClient):
    
    def __init__(self, uri):
        self.uri = uri
        self.local_id = getuuid()
        self.handler_id = None
        #WebSocketClient.__init__(self, url=uri, heartbeat_freq=10)
        WebSocketClient.__init__(self, url=uri)


    def prompt(self):
        return 'DiarizationClient(%s, %s) ' % (str(self.local_id), str(self.handler_id))


    def opened(self):
        logging.info(self.prompt() + ' connection is open ')


    def closed(self, code, reason=None):
        logging.info(self.prompt() + ' has closed down ' + str(code) + str(reason))


    def received_message(self, m):
        #logging.info(self.prompt() + ' => %d %s' % (len(m), str(m)))
        logging.info(self.prompt() + ' => %d' % (len(m)))


def main():

    logging.basicConfig(level=logging.INFO, format="%(levelname)8s %(asctime)s %(message)s ")
    logging.debug('Starting up diarization worker')
    parser = argparse.ArgumentParser(description='Diarization worker')
    parser.add_argument('-u', '--uri', default="ws://localhost:8888/diarization/", \
                    dest="uri", help="Server<-->diarization worker websocket URI")
    parser.add_argument('-m', '--manager', dest="manager", 
                    help="Client manager PID", required=True)
    args = parser.parse_args()

    args.uri = args.uri + '?%s' % (urllib.urlencode([('manager_id', args.manager)]))

    try:
        diarization_client = DiarizationClient(args.uri)
        diarization_client.connect()

        diarization_client.run_forever()
    except KeyboardInterrupt:
        diarization_client.close()



if __name__ == '__main__':
    main()

