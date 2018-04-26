import sys, os
from ws4py.client.threadedclient import WebSocketClient
import logging
import argparse
import json

from bluetooth import *
import subprocess

uuid = "94f39d29-7d6d-437d-973b-fba39e49d4ee"

class CameraClient(WebSocketClient):

    def __init__(self, uri):
        self.uri = uri
        self.local_id = 'CAMERA_CONTROL_CLIENT'
        self.manager_id = None
        self.handler_id = None
        #WebSocketClient.__init__(self, url=uri, heartbeat_freq=10)
        WebSocketClient.__init__(self, url=uri)


    def prompt(self):
        return 'UIClient(%s, %s, %s) ' % (str(self.local_id), str(self.handler_id), str(self.manager_id))


    def opened(self):
        pass


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


def camera_command_line(camera_client):
    do_loop = True
    while do_loop:
        string = raw_input('(CAMERA) Enter command: ')
        splits = string.strip().split(' ')
        for i, s in enumerate(splits):
            splits[i] = s.lower()

        if 'quit' in splits:
            do_loop = False
        elif splits[0] == 'send':
            camera_client.send(' '.join(splits[1:]))
        else:
            print('Unrecognized command ' + str(string))

    camera_client.close()


def rfcomm_loop(camera_client):

    count = 0
    while True:
        
        count += 1
        logging.info('RFComm round %d' % count)

        subprocess.call(['sudo', 'hciconfig', 'hci0', 'piscan'])
        # make pi discoverable
        server_sock=BluetoothSocket( RFCOMM )
        server_sock.bind(("",PORT_ANY))
        server_sock.listen(1)

        port = server_sock.getsockname()[1]

        advertise_service( server_sock, "SampleServer",
                        service_id = uuid,
                        service_classes = [ uuid, SERIAL_PORT_CLASS ],
                        profiles = [ SERIAL_PORT_PROFILE ], 
        #                   protocols = [ OBEX_UUID ] 
                        )
                        
        logging.info("Waiting for connection on RFCOMM channel %d" % port)

        client_sock, client_info = server_sock.accept()
        logging.info("Accepted connection from ", client_info)

        try:
            while True:
                data = client_sock.recv(1024)
                if len(data) == 0: break
                logging.info("received [%s]" % data)

                data = data.lower()
                data = data.strip()
                camera_client.send(data)

        except IOError:
            pass

        logging.info("Bluetooth disconnected")

        client_sock.close()
        server_sock.close()
        logging.info("all done")



def main():

    logging.basicConfig(level=logging.INFO, format="%(levelname)8s %(asctime)s %(message)s ")
    logging.info('Starting up CAMERA client')
    parser = argparse.ArgumentParser(description='Command line based camera control')
    parser.add_argument('-u', '--uri', default="ws://localhost:8000/control", \
                    dest="uri", help="Server<-->camera control websocket URI")
    parser.add_argument('-p', '--ip', default=None, \
                    dest="ip", help="Server IP address")                    
    #parser.add_argument('-m', '--manager', dest="manager", 
    #                help="Client manager PID", required=True)
    args = parser.parse_args()

    if args.ip is not None:
        args.uri = 'ws://' + str(args.ip.strip()) + ':8000/control'

    try:
        camera_client = CameraClient(args.uri)
        camera_client.connect()

        #camera_command_line(camera_client)
        rfcomm_loop(camera_client)
    except KeyboardInterrupt:
        camera_client.close()



if __name__ == '__main__':
    main()

