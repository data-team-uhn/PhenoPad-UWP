#!/usr/bin/env python
#

"""
Reads speech data via websocket requests, sends it to Redis, waits for results from Redis and
forwards to client via websocket
"""
import sys, os
PARENT_DIRECTORY = os.path.dirname(os.path.abspath(__file__))
CLIENT_DIRECTORY = os.path.join(PARENT_DIRECTORY, '..', 'client')
SERVER_DIRECTORY = os.path.join(PARENT_DIRECTORY, '..', 'server')
CONFIG_DIRECTORY = os.path.join(PARENT_DIRECTORY, '..', 'configs')
SCRIPT_DIRECTORY = os.path.join(PARENT_DIRECTORY, '..', 'scripts')
sys.path.append(CLIENT_DIRECTORY)
sys.path.append(SERVER_DIRECTORY)

import logging
import json
import codecs
import os.path
import uuid
import time
import threading
import functools
import random
import urllib
from Queue import Queue

import tornado.ioloop
import tornado.options
import tornado.web
import tornado.websocket
import tornado.gen
import tornado.concurrent
from tornado.httpclient import AsyncHTTPClient
import settings
import common
from common import getuuid

from multiprocessing import Lock

# Custom imports
import client_rest as cr
from serverside_handlers import *
from clientside_handlers import * 
from filemanager import *


def run_async(func):
    @functools.wraps(func)
    def async_func(*args, **kwargs):
        func_hl = threading.Thread(target=func, args=args, kwargs=kwargs)
        func_hl.start()
        return func_hl

    return async_func



class Application(tornado.web.Application):
    def __init__(self):
        settings = dict(
            cookie_secret="43oETzKXQAGaYdkL5gEmGeJJFuYh7EQnp2XdTP1o/Vo=",
            template_path=os.path.join(os.path.dirname(os.path.dirname(__file__)), "templates"),
            static_path=os.path.join(os.path.dirname(os.path.dirname(__file__)), "static"),
            xsrf_cookies=False,
            autoescape=None,
        )

        handlers = [
            (r"/", MainHandler),

            # A "hub" that coordinates messages
            (r"/client_manager", ClientManagerSocketHandler),
            (r"/file_manager/post", FileManagerPostRequestHandler),
            (r"/file_manager/get/(.*)", FileManagerGetRequestHandler),
            (r"/file_manager/put/(.*)", FileManagerPutRequestHandler),

            # Connections from external sources (Surface tablet and Raspberry Pi)
            (r"/ui", UISocketHandler),
            (r"/audio", AudioSocketHandler),
            (r"/tracking", TrackingSocketHandler),
            
            # ASR and Diarization workers
            (r"/asr", ASRSocketHandler),
            (r"/diarization", DiarizationSocketHandler),
            (r"/odas", ODASSocketHandler),

            # Not sure what this is ;P
            (r"/client/static/(.*)", tornado.web.StaticFileHandler, {'path': settings["static_path"]}),
        ]
        tornado.web.Application.__init__(self, handlers, **settings)

        self.current_managers = {}
        self.current_ui_handlers = {}

        self.num_requests_processed = 0

        self.worker_request_lock = Lock()

        logging.info('Server initialized')


    def save_reference(self, content_id, content):
        refs = {}
        try:
            with open("reference-content.json") as f:
                refs = json.load(f)
        except:
            pass
        refs[content_id] = content
        with open("reference-content.json", "w") as f:
            json.dump(refs, f, indent=2)


class MainHandler(tornado.web.RequestHandler):
    def get(self):
        current_directory = os.path.dirname(os.path.abspath(__file__))
        parent_directory = os.path.join(current_directory, os.pardir)
        readme = os.path.join(parent_directory, "README.md")
        self.render(readme)


class ClientManagerSocketHandler(tornado.websocket.WebSocketHandler):

    def __init__(self, application, request, **kwargs):
        tornado.websocket.WebSocketHandler.__init__(self, application, request, **kwargs)
        self.id = getuuid()
        self.initialize_socket()


    # needed for Tornado 4.0
    def check_origin(self, origin):
        return True


    def initialize_socket(self):
        self.server_side = ['ASR', 'Diarization', 'ODAS']
        self.client_side = ['UI', 'Audio', 'Tracking']
        self.managed_handlers = {   'ASR': None, 
                                    'Diarization': None, 
                                    'FileManager': None,
                                    'ODAS': None, 
                                    'Tracking': None, 
                                    'Audio': None, 
                                    'UI': None}


    def close_managed_handlers(self):
        for s in self.managed_handlers:
            if self.managed_handlers[s] is not None:
                try:
                    self.managed_handlers[s].close()
                except:
                    logging.warning('Cannot close %s handler' % s)    


    def open(self):
        self.initialize_socket()

        ui_handler_id = self.get_argument("ui_handler", None, True)
        if ui_handler_id:
            logging.info("%s: UI handler ID is : %s" % (self.id, ui_handler_id))
            self.set_managed_handlers('UI', self.application.current_ui_handlers[ui_handler_id])
            self.managed_handlers['UI'].manager = self
        else:
            logging.fatal('%s: Unable to obtain UI handler ID' % (self.id))
            exit()
            
        self.application.current_managers[self.id] = self
        new_manager = cr.create_manager(self.id, {'ui_pid': ui_handler_id})['info']
        logging.info(new_manager)
        assert new_manager['manager_pid'] == self.id and new_manager['ui_pid'] == ui_handler_id
        logging.info("%s: New manager available " % (self.id))

        logging.info('%s: Preparing ASR, Diarization and ODAS worker' % (self.id))
        
        os.system('bash ' + os.path.join(SCRIPT_DIRECTORY, 'launch_asr.sh') + ' ' + self.id)
        time.sleep(1)
        os.system('bash ' + os.path.join(SCRIPT_DIRECTORY, 'launch_diarization.sh') + ' ' + self.id)
        time.sleep(1)
        #os.system('bash ' + os.path.join(SCRIPT_DIRECTORY, 'launch_filemanager.sh') + ' ' + self.id)
        #time.sleep(1)
        os.system('bash ' + os.path.join(SCRIPT_DIRECTORY, 'launch_odas.sh') + ' ' + self.id)
        time.sleep(1)

        self.relay_message('UI', json.dumps({'manager_id': self.id}))


    def on_connection_close(self):
        logging.info("Manager " + str(self.id) + " is leaving")
        print(self.application.current_managers.pop(self.id))
        self.close_managed_handlers()


    def on_message(self, message):
        logging.info('%s: Received %s' % (self.id, str(message)))


    def set_managed_handlers(self, handler_name, handler_instance):
        if handler_name not in self.managed_handlers:
            logging.error('ERROR! ' + handler_name + ' is NOT allowed!')
        self.managed_handlers[handler_name] = handler_instance

        self.check_ready()


    def relay_message(self, handler_name, message, is_binary=False):

        logging.info('Relaying message for ' + str(handler_name))
        try:
            self.managed_handlers[handler_name].write_message(message, binary=is_binary)
        except Exception as e:
            logging.warn('%s: Manager cannot relay message to %s\n%s' % (self.id, handler_name, str(e)))


    def check_ready(self):
        is_ready = True
        not_ready = []
        client_not_ready = []
        for s in self.server_side:
            # Audio must not be connected until server is ready
            if self.managed_handlers[s] == None:
                not_ready.append(s)
                is_ready = False
        
        for s in self.client_side:
            # Audio must not be connected until server is ready
            if self.managed_handlers[s] == None:
                client_not_ready.append(s)
        
        logging.warning('Handlers ' + str(not_ready) + ' not ready yet')
        logging.warning('Client connections ' + str(logging.warning('Handlers ' + str(not_ready) + ' not ready yet')) + ' not ready yet')
        
        self.relay_message('UI', json.dumps({'server_status': \
                    {'ready': is_ready, 'waiting_for_server': not_ready, 'waiting_for_client': client_not_ready}}))
        



###### Functions for file manager ######

SIZE_KB=1024
SIZE_MB=SIZE_KB*SIZE_KB
SIZE_GB=SIZE_KB*SIZE_KB*SIZE_KB
SIZE_TB=SIZE_KB*SIZE_KB*SIZE_KB*SIZE_KB


class FileManagerException(tornado.web.HTTPError):
    pass



class FileManagerPostRequestHandler(tornado.web.RequestHandler):
    client = AsyncHTTPClient(max_buffer_size=SIZE_GB)

    def prepare(self):
        self.id = getuuid()

        self.user_id = self.request.headers.get("user-id", None)
        self.content_path = self.request.headers.get("content-path", None)
        self.manager_id = str(self.request.headers.get("manager-id", None))

        logging.info("%s: OPEN: user='%s', content='%s', id='%s'" % \
                        (self.id, self.user_id, self.content_path, self.manager_id))

        if self.user_id is None:
            raise FileManagerException(status_code=406, reason='User ID cannot be None')
        self.request.connection.set_max_body_size(SIZE_GB)
            

    def post(self):
        #logging.info('EXTRA STUFF IS ' + str(args))
        #logging.info(str(self.request.files))
        for field_name, files in self.request.files.items():
            for info in files:
                filename, content_type = info['filename'], info['content_type']
                body = info['body']
                logging.info('POST "%s" "%s" %d bytes',
                             filename, content_type, len(body))
                status, desired_path = find_appropriate_path(filename)

                logging.info('Finishing up')
                if status is False:
                    #fm_stream_post_finish_user_all(self.user_id, content=body)
                    raise FileManagerException(status_code=406, reason=desired_path)
                else:
                    fm_put(self.user_id, path=desired_path, content=body)

        self.write('OK')




@tornado.web.stream_request_body
class FileManagerPutRequestHandler(tornado.web.RequestHandler):
    client = AsyncHTTPClient(max_buffer_size=SIZE_GB)

    def prepare(self):
        self.id = getuuid()

        self.user_id = self.request.headers.get("user-id", None)
        self.manager_id = str(self.request.headers.get("manager-id", None))

        logging.info("%s: OPEN: user='%s', id='%s'" % \
                        (self.id, self.user_id, self.manager_id))

        if self.user_id is None:
            raise FileManagerException(status_code=406, reason='User ID cannot be None')
        
        self.bytes_read = 0
        self.f, self.temp_path = fm_stream_put_prep(self.user_id)
        self.request.connection.set_max_body_size(SIZE_GB)


    def data_received(self, chunk):
        self.bytes_read += len(chunk)
        fm_stream_put(self.f, chunk)
        logging.info('%s: Accumulated size: %d' % (self.id, self.bytes_read))


    def put(self, filename):
        filename = urllib.unquote(filename)
        logging.info('Receving ' + str(filename))
        mtype = self.request.headers.get('Content-Type')
        logging.info('PUT "%s" "%s" %d bytes', filename, mtype, self.bytes_read)

        status, desired_path = find_appropriate_path(filename)
        logging.info('Finishing up')
        if status is False:
            fm_stream_put_finish_user_all(self.user_id, temp_path=self.temp_path)
            #raise FileManagerException(status_code=406, reason=desired_path)
        else:
            fm_stream_put_finish(self.user_id, path=desired_path, temp_path=self.temp_path)

        self.write('OK')



class FileManagerGetRequestHandler(tornado.web.RequestHandler):
    client = AsyncHTTPClient(max_buffer_size=SIZE_GB)

    def prepare(self):
        self.id = getuuid()

        self.user_id = self.request.headers.get("user-id", None)
        self.manager_id = str(self.request.headers.get("manager-id", None))

        logging.info("%s: OPEN: user='%s', id='%s'" % \
                        (self.id, self.user_id, self.manager_id))

        if self.user_id is None:
            raise FileManagerException(status_code=406, reason='User ID cannot be None')
        
        self.bytes_read = 0
        self.f, self.temp_path = fm_stream_put_prep(self.user_id)
        self.request.connection.set_max_body_size(SIZE_GB)

    
    @tornado.web.asynchronous
    @tornado.gen.engine
    def get(self, filename):
        filename = urllib.unquote(filename)
        logging.info('Sending ' + str(filename))
        mtype = self.request.headers.get('Content-Type')
        logging.info('GET "%s" "%s"' % (filename, mtype))

        status, desired_path = find_appropriate_path(filename)
        if status is False:
            #raise FileManagerException(status_code=406, reason=desired_path)
            logging.info('Did not detect path for a particular file, packging all files from user')
            self.opened_file = fm_get_user_all(self.user_id)
        else:
            logging.info('Running get for %s' % (desired_path))
            self.write_byte_count = 0
            self.opened_file = fm_get(self.user_id, path=desired_path)
        
        total_size = 0
        while True:
            data = self.opened_file.read(SIZE_MB)
            if not data:
                break
            total_size += len(data)
            logging.info('Total read size %d' % total_size)
            self.write(data)
            self.flush()
            time.sleep(0.01)
        self.opened_file.close()
        self.flush()
        logging.info('Finished flushing')
        #time.sleep()
        self.finish()
        logging.info('Finished')


    '''
    @tornado.web.asynchronous
    def get(self, filename):
        filename = urllib.unquote(filename)
        logging.info('Sending ' + str(filename))
        mtype = self.request.headers.get('Content-Type')
        logging.info('GET "%s" "%s"' % (filename, mtype))

        status, desired_path = find_appropriate_path(filename)
        if status is False:
            raise FileManagerException(status_code=406, reason=desired_path)
        #fm_stream_put_finish(self.user_id, path=desired_path, temp_path=self.temp_path)

        logging.info('Running get for %s' % (desired_path))
        self.write_byte_count = 0
        self.opened_file = fm_get(self.user_id, path=desired_path)
        self.flush()
        self.write_more()

    def write_more(self):
        data = self.opened_file.read(SIZE_MB)
        if not data:
            self.finish()
            self.opened_file.close()
            return
        self.write(data)
        self.flush(callback=self.write_more)
    '''


def main():

    print("Loading server")
    sys.stdout.flush()

    logging.basicConfig(level=logging.INFO, format="%(levelname)8s %(asctime)s %(message)s ")
    logging.info('Server is starting')

    from tornado.options import define, options
    define("certfile", default="", help="certificate file for secured SSL connection")
    define("keyfile", default="", help="key file for secured SSL connection")

    tornado.options.parse_command_line()
    app = Application()
    if options.certfile and options.keyfile:
        ssl_options = {
          "certfile": options.certfile,
          "keyfile": options.keyfile,
        }
        logging.info("Using SSL for serving requests")
        app.listen(options.port, ssl_options=ssl_options)
    else:
        print("Listening to " + str(options.port))
        app.listen(options.port)
    tornado.ioloop.IOLoop.instance().start()


if __name__ == "__main__":
    main()
