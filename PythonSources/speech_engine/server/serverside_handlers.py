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



class ASRSocketHandler(tornado.websocket.WebSocketHandler):

    def __init__(self, application, request, **kwargs):
        tornado.websocket.WebSocketHandler.__init__(self, application, request, **kwargs)
        self.id = getuuid()
        self.manager_id = None
        self.manager = None

    # needed for Tornado 4.0
    def check_origin(self, origin):
        return True


    def open(self):
        logging.info("ASR worker @%s is opening socket", str(self.id))
        manager_id = self.get_argument("manager_id", None, True)
        if manager_id:
            logging.info("%s: Manager ID is : %s" % (self.id, manager_id))
            self.manager_id = str(manager_id)
            self.manager = self.application.current_managers[manager_id]
            self.manager.set_managed_handlers('ASR', self)
        else:
            logging.fatal('%s: Unable to obtain manager ID' % (self.id))
            exit()

        new_manager = cr.update_manager(manager_id, {'asr_pid': self.id})['info']
        logging.info(new_manager)
        logging.info(len(self.manager_id))
        logging.info(len(new_manager['manager_pid']))
        assert (new_manager['manager_pid'] == self.manager_id)\
                 and (new_manager['asr_pid'] == self.id)

        logging.info("ASR worker @%s fully initialized", str(self.id))

    def on_close(self):
        logging.info("ASR worker @%s is closing socket", self.id)


    def on_message(self, message):
        #self.manager.write_message(json.dumps({'message_type': 'ASR', 'message': message}))
        logging.info('ASR RESULT: ' + str(message))
        


class DiarizationSocketHandler(tornado.websocket.WebSocketHandler):

    def __init__(self, application, request, **kwargs):
        tornado.websocket.WebSocketHandler.__init__(self, application, request, **kwargs)
        self.id = getuuid()
        self.manager_id = None
        self.manager = None


    # needed for Tornado 4.0
    def check_origin(self, origin):
        return True


    def open(self):
        logging.info("Diarization worker @%s is opening socket", str(self.id))
        
        manager_id = self.get_argument("manager_id", None, True)
        if manager_id:
            logging.info("%s: Manager ID is : %s" % (self.id, manager_id))
            self.manager_id = str(manager_id)
            self.manager = self.application.current_managers[manager_id]
            self.manager.set_managed_handlers('Diarization', self)
        else:
            logging.fatal('%s: Unable to obtain manager ID' % (self.id))
            exit()

        new_manager = cr.update_manager(manager_id, {'diarization_pid': self.id})['info']
        assert (new_manager['manager_pid'] == self.manager_id) and \
                (new_manager['diarization_pid'] == self.id)

        logging.info("Diarization worker @%s fully initialized", str(self.id))

    def on_close(self):
        logging.info("Diarization worker @%s is closing socket", self.id)


    def on_message(self, message):
        self.manager.write_message({'message_type': 'Diarization', 'message': message})



class ODASSocketHandler(tornado.websocket.WebSocketHandler):

    def __init__(self, application, request, **kwargs):
        tornado.websocket.WebSocketHandler.__init__(self, application, request, **kwargs)
        self.id = getuuid()
        self.manager_id = None
        self.manager = None


    # needed for Tornado 4.0
    def check_origin(self, origin):
        return True


    def open(self):
        logging.info("ODAS worker @%s is opening socket", str(self.id))
        
        manager_id = self.get_argument("manager_id", None, True)
        if manager_id:
            logging.info("%s: Manager ID is : %s" % (self.id, manager_id))
            self.manager_id = str(manager_id)
            self.manager = self.application.current_managers[manager_id]
            self.manager.set_managed_handlers('ODAS', self)
        else:
            logging.fatal('%s: Unable to obtain manager ID' % (self.id))
            exit()

        new_manager = cr.update_manager(manager_id, {'odas_pid': self.id})['info']
        assert (new_manager['manager_pid'] == self.manager_id) and \
                (new_manager['odas_pid'] == self.id)

        logging.info("ODAS worker @%s fully initialized", str(self.id))

    def on_close(self):
        logging.info("ODAS worker @%s is closing socket", self.id)


    def on_message(self, message):
        # TODO!
        # Parse message as JSON or some format that distinguishes between message 
        # for tracking message for audio
        #
        # then use existing code to combine audio into 1 track

        #self.manager.relay_message('ASR', some_message, is_binary=True)
        #self.manager.relay_message('Diarization', some_message, is_binary=True)

        pass
