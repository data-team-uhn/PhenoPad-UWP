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

from common import getuuid


class UIClient(WebSocketClient):

    def __init__(self, uri):
        self.uri = uri
        self.local_id = getuuid()
        self.manager_id = None
        self.handler_id = None
        #WebSocketClient.__init__(self, url=uri, heartbeat_freq=10)
        WebSocketClient.__init__(self, url=uri)


    def prompt(self):
        return 'UIClient(%s, %s, %s) ' % (str(self.local_id), str(self.handler_id), str(self.manager_id))


    def opened(self):
        for i in range(1):
            self.send(self.prompt() + ' sending useless content %d' % i)


    def closed(self, code, reason=None):
        logging.info(self.prompt() + ' has closed down ' + str(code) + str(reason))


    def received_message(self, m):
        logging.info(self.prompt() + ' => msg length=%d ' % (len(m)))
        
        try:
            parsed = json.loads(str(m))
        except Exception as e:
            logging.warn('Cannot parse %s \n Error %s' % (str(m), str(e)))
            parsed = None
        
        if 'handler_id' in parsed:
            self.handler_id = parsed['handler_id']
            logging.info(self.prompt() + ' server handler: %s' % self.handler_id)

        if 'manager_id' in parsed:
            self.manager_id = parsed['manager_id']
            logging.info(self.prompt() + ' client manager: %s' % self.manager_id)
        
        else:
            logging.info(self.prompt() + ' ' + str(m))


def ui_command_line(ui_client):
    do_loop = True
    while do_loop:
        string = raw_input('(UI) Enter command: ')
        splits = string.strip().split(' ')
        for i, s in enumerate(splits):
            splits[i] = s.lower()

        if 'quit' in splits:
            do_loop = False
        elif splits[0] == 'send':
            ui_client.send(' '.join(splits[1:]))
        else:
            print('Unrecognized command ' + str(string))

    ui_client.close()



def main():

    logging.basicConfig(level=logging.INFO, format="%(levelname)8s %(asctime)s %(message)s ")
    logging.info('Starting up UI client')
    parser = argparse.ArgumentParser(description='Command line based UI')
    parser.add_argument('-u', '--uri', default="ws://localhost:8888/ui", \
                    dest="uri", help="Server<-->UI websocket URI")
    parser.add_argument('-p', '--ip', default=None, \
                    dest="ip", help="Server IP address")                    
    #parser.add_argument('-m', '--manager', dest="manager", 
    #                help="Client manager PID", required=True)
    args = parser.parse_args()

    if args.ip is not None:
        args.uri = 'ws://' + str(args.ip.strip()) + ':8888/ui'

    try:
        ui_client = UIClient(args.uri)
        ui_client.connect()

        ui_command_line(ui_client)
    except KeyboardInterrupt:
        ui_client.close()



if __name__ == '__main__':
    main()

