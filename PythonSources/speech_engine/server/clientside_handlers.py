#!/usr/bin/env python

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
from Queue import Queue

import tornado.ioloop
import tornado.options
import tornado.web
import tornado.websocket
import tornado.gen
import tornado.concurrent
import settings
import common
from common import getuuid

from multiprocessing import Lock

# Custom imports
import client_rest as cr




class UISocketHandler(tornado.websocket.WebSocketHandler):

    def __init__(self, application, request, **kwargs):
        tornado.websocket.WebSocketHandler.__init__(self, application, request, **kwargs)
        self.id = getuuid()
        self.manager = None


    # needed for Tornado 4.0
    def check_origin(self, origin):
        return True


    def send_event(self, event):
        event["id"] = str(self.id)
        event_str = str(event)
        if len(event_str) > 100:
            event_str = event_str[:97] + "..."
        #logging.info("%s: Sending event %s to client" % (self.id, event_str))
        self.write_message(json.dumps(event))


    def open(self):
        logging.info("%s: OPEN" % str(self.id))
        logging.info("%s: Request arguments: %s" % \
                (str(self.id), " ".join(["%s=\"%s\"" % (a, self.get_argument(a)) for a in self.request.arguments])))
        self.user_id = self.get_argument("user-id", "none", True)
        self.content_id = self.get_argument("content-id", "none", True)
        
        self.application.current_ui_handlers[self.id] = self

        # manager might not be ready when client initiates request
        self.manager = None
        self.obtain_manager()
        logging.info("%s: UI connection opened. Manager launch probably in progress" % str(self.id))
        self.write_message(json.dumps({'handler_id': self.id}))
        

    def obtain_manager(self):
        
        self.accumulated_messages = None
        
        logging.warn("%s: We will prepare worker for you" % str(self.id))
        
        managers = cr.get_all_managers()

        cap = 1
        with open(os.path.join(CONFIG_DIRECTORY, 'WORKER_CAP'), 'r') as f:
            cap = int(f.read()) + 1

        # Only terminate connection when capacity has been reached
        if len(managers) >= cap:
            event = dict(status=common.STATUS_NOT_AVAILABLE, 
                message="No decoder available, already have " + str(len(managers) - 1) + ". Try again later..")
            self.send_event(event)
            self.close()
        else:
            logging.info('%s: Manager count: %d/%d' % (self.id, len(managers), cap - 1))
            os.system("bash " + os.path.join(SCRIPT_DIRECTORY, 'launch_manager.sh') + ' ' + self.id)
            time.sleep(1)
            logging.info("%s: executing script to start manager" % str(self.id))


    def on_close(self):
        logging.info("%s: Handling on_close()" % str(self.id))
        self.application.num_requests_processed += 1
        if self.manager:
            self.manager.close()
            logging.info("%s: (UI)Handling on_close()" % str(self.id))


    def on_message(self, message):

        logging.info('%s: Received %s' % (self.id, str(message)))

        if self.accumulated_messages is None:
            self.accumulated_messages = [message]
        else:
            self.accumulated_messages.append(message)
        

        if self.manager is None:
            logging.warn("%s: No manager available. message length is %s" % (self.id, str(len(message))))
        else:
            #logging.info("%s: Forwarding client message (%s) of length %d to worker" % (self.id, type(message), len(message)))
            for m in self.accumulated_messages:
                if isinstance(m, unicode):
                    self.manager.write_message(m, binary=False)
                else:
                    self.manager.write_message(m, binary=True)

            self.accumulated_messages = None
        


class AudioSocketHandler(tornado.websocket.WebSocketHandler):

    def __init__(self, application, request, **kwargs):
        tornado.websocket.WebSocketHandler.__init__(self, application, request, **kwargs)
        self.id = getuuid()
        self.manager_id = None
        self.manager = None


    # needed for Tornado 4.0
    def check_origin(self, origin):
        return True


    def send_event(self, event):
        event["id"] = self.id
        event_str = str(event)
        if len(event_str) > 100:
            event_str = event_str[:97] + "..."
        #logging.info("%s: Sending event %s to client" % (self.id, event_str))
        self.write_message(json.dumps(event))
        

    def open(self):
        logging.info("Audio client @%s is opening socket", self.id)
        manager_id = self.get_argument("manager_id", None, True)
        self.user_id = self.get_argument("user-id", "none", True)
        self.content_id = self.get_argument("content-id", "none", True)
        
        if manager_id:
            logging.info("%s: Manager ID is : %s" % (self.id, manager_id))
            self.manager_id = str(manager_id)
            self.manager = self.application.current_managers[manager_id]
            self.manager.set_managed_handlers('Audio', self)
        else:
            logging.fatal('%s: Unable to obtain manager ID' % (self.id))
            exit()

        content_type = self.get_argument('content-type', None, True)
        if content_type:
            logging.info("%s: Content type is : %s" % (self.id, content_type))
            self.manager.relay_message('ASR', \
                            json.dumps(dict(id=self.id, content_type=content_type, \
                            user_id=self.user_id, content_id=self.content_id)))
        else:
            logging.fatal('%s: Unable to obtain manager ID' % (self.id))
            exit()

        new_manager = cr.update_manager(manager_id, {'audio_pid': self.id})['info']
        assert new_manager['manager_pid'] == self.manager_id and new_manager['audio_pid'] == self.id



    def on_close(self):
        logging.info("%s: (Audio)Handling on_close()" % str(self.id))


    def on_message(self, message):
        is_binary = not isinstance(message, unicode)
        #self.manager.relay_message('ASR', message, is_binary=is_binary)
        #self.manager.relay_message('Diarization', message,  is_binary=is_binary)
        self.manager.relay_message('ODAS', json.dumps({'message_type': 'Audio', 'message': message}))



class TrackingSocketHandler(tornado.websocket.WebSocketHandler):

    def __init__(self, application, request, **kwargs):
        tornado.websocket.WebSocketHandler.__init__(self, application, request, **kwargs)
        self.id = getuuid()
        self.manager_id = None
        self.manager = None


    # needed for Tornado 4.0
    def check_origin(self, origin):
        return True


    def open(self):
        logging.info("Tracking worker @%s is opening socket", str(self.id))
        
        manager_id = self.get_argument("manager_id", None, True)
        if manager_id:
            logging.info("%s: Manager ID is : %s" % (self.id, manager_id))
            self.manager_id = str(manager_id)
            self.manager = self.application.current_managers[manager_id]
            self.manager.set_managed_handlers('Tracking', self)
        else:
            logging.fatal('%s: Unable to obtain manager ID' % (self.id))
            exit()

        new_manager = cr.update_manager(manager_id, {'tracking_pid': self.id})['info']
        assert (new_manager['manager_pid'] == self.manager_id) and \
                (new_manager['tracking_pid'] == self.id)

        logging.info("Tracking worker @%s fully initialized", str(self.id))

    def on_close(self):
        logging.info("Tracking worker @%s is closing socket", self.id)


    def on_message(self, message):
        self.manager.relay_message('ODAS', json.dumps({'message_type': 'Tracking', 'message': message}))


