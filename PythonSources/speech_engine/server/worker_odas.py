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


class ODASClient(WebSocketClient):

    def __init__(self, uri):
        self.uri = uri
        self.local_id = getuuid()
        self.handler_id = None
        #WebSocketClient.__init__(self, url=uri, heartbeat_freq=10)
        WebSocketClient.__init__(self, url=uri)


    def prompt(self):
        return 'ODASClient(%s, %s) ' % (str(self.local_id), str(self.handler_id))


    def opened(self):
        logging.info(self.prompt() + ' connection is open')


    def closed(self, code, reason=None):
        logging.info(self.prompt() + ' has closed down ' + str(code) + str(reason))


    def received_message(self, m):
        logging.info(self.prompt() + ' => %d %s' % (len(m), str(m)))



def main():

    logging.basicConfig(level=logging.INFO, format="%(levelname)8s %(asctime)s %(message)s ")
    logging.debug('Starting up odas client for combining audio')
    parser = argparse.ArgumentParser(description='ODAS worker')
    parser.add_argument('-u', '--uri', default="ws://localhost:8888/odas", \
                    dest="uri", help="Server<-->ODAS worker")
    parser.add_argument('-m', '--manager', dest="manager", 
                    help="Client manager PID", required=True)
    args = parser.parse_args()

    args.uri = args.uri + '?%s' % (urllib.urlencode([('manager_id', args.manager)]))
   

    try:
        odas_client = ODASClient(args.uri)
        odas_client.connect()

        odas_client.run_forever()
    except KeyboardInterrupt:
        odas_client.close()


if __name__ == '__main__':
    main()

