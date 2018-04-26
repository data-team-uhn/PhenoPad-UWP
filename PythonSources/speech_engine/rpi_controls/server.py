#!/usr/bin/env python
"""
Creates an HTTP server with basic auth and websocket communication.
"""
import argparse
import base64
import hashlib
import os
import time
import threading
import webbrowser
import logging

try:
    import cStringIO as io
except ImportError:
    import io

import tornado.web
import tornado.websocket
from tornado.ioloop import PeriodicCallback

# Hashed password for comparison and a cookie for login cache
ROOT = os.path.normpath(os.path.dirname(__file__))
with open(os.path.join(ROOT, "password.txt")) as in_file:
    PASSWORD = in_file.read().strip()
COOKIE_NAME = "camp"


class Application(tornado.web.Application):
    def __init__(self):
        handlers = [(r"/", IndexHandler),
                (r"/websocket", WebSocket),
                (r"/control", ControlSocketHandler),
                (r"/static/password.txt", ErrorHandler),
                (r'/static/(.*)', tornado.web.StaticFileHandler, {'path': ROOT})]
        tornado.web.Application.__init__(self, handlers)

        logging.info('Server initialized')
        self.camera_socket = None


class IndexHandler(tornado.web.RequestHandler):

    def get(self):
        if args.require_login and not self.get_secure_cookie(COOKIE_NAME):
            self.redirect("/login")
        else:
            self.render("index.html", port=args.port)


class ErrorHandler(tornado.web.RequestHandler):
    def get(self):
        self.send_error(status_code=403)


STATUS_RECORD = 1
STATUS_STOP = 2
STATUS_PICTURE = 3


class WebSocket(tornado.websocket.WebSocketHandler):

    def open(self):
        logging.info('Camera server is opening websocket')
        self.status = STATUS_RECORD
        self.application.camera_socket = self


    def on_message(self, message):
        """Evaluates the function pointed to by json-rpc."""

        # Start an infinite loop when this is called
        if message == "read_camera":
            if not args.require_login or self.get_secure_cookie(COOKIE_NAME):
                self.camera_loop = PeriodicCallback(self.loop, 10)
                self.camera_loop.start()
            else:
                print("Unauthenticated websocket request")

        # Extensibility for other methods
        else:
            print("Unsupported function: " + message)


    def loop(self):
        """Sends camera images in an infinite loop."""
        sio = io.StringIO()

        #if args.use_usb:
        #    _, frame = camera.read()
        #    img = Image.fromarray(cv2.cvtColor(frame, cv2.COLOR_BGR2RGB))
        #    img.save(sio, "JPEG")
        #else:

        if self.status == STATUS_RECORD or self.status == STATUS_PICTURE:
            camera.capture(sio, "jpeg", use_video_port=True)

            if self.status == STATUS_PICTURE:
                self.status = STATUS_RECORD
                logging.info('Canera status is ' + str(self.status))
                with open('/home/pi/picture.jpeg', 'w') as f:
                    f.write(sio.getvalue())
            try:
                self.write_message(base64.b64encode(sio.getvalue()))
            except tornado.websocket.WebSocketClosedError:
                self.camera_loop.stop()
        elif self.status == STATUS_STOP:
            logging.info('Not doing anything')
            time.sleep(2)


class ControlSocketHandler(tornado.websocket.WebSocketHandler):

    def __init__(self, application, request, **kwargs):
        tornado.websocket.WebSocketHandler.__init__(self, application, request, **kwargs)
        self.id = 'CCAMERA_ONTROLER'

    # needed for Tornado 4.0
    def check_origin(self, origin):
        return True


    def open(self):
        logging.info("Controller @%s is opening socket", str(self.id))
        

    def on_close(self):
        logging.info("Controller worker @%s is closing socket", self.id)


    def on_message(self, message):
        logging.info("Message: " + str(message))

        message = message.lower()
        if message == 'start':
            self.application.camera_socket.status = STATUS_RECORD
        elif message == 'stop':
            self.application.camera_socket.status = STATUS_STOP
        elif message == 'picture':
            self.application.camera_socket.status = STATUS_PICTURE
        


def main():
  
    application = Application()
    application.listen(args.port)

    #webbrowser.open("http://localhost:%d/" % args.port, new=2)

    tornado.ioloop.IOLoop.instance().start()


if __name__ == '__main__':

    logging.basicConfig(level=logging.INFO, format="%(levelname)8s %(asctime)s %(message)s ")
    logging.info('Server is starting')

    parser = argparse.ArgumentParser(description="Starts a webserver that "
                                    "connects to a webcam.")
    parser.add_argument("--port", type=int, default=8000, help="The "
                        "port on which to serve the website.")
    parser.add_argument("--resolution", type=str, default="low", help="The "
                        "video resolution. Can be high, medium, or low.")
    parser.add_argument("--require-login", action="store_true", help="Require "
                        "a password to log in to webserver.")
    parser.add_argument("--use-usb", action="store_true", help="Use a USB "
                        "webcam instead of the standard Pi camera.")
    parser.add_argument("--usb-id", type=int, default=0, help="The "
                        "usb camera number to display")
    args = parser.parse_args()

    if args.use_usb:
        import cv2
        from PIL import Image
        camera = cv2.VideoCapture(args.usb_id)
    else:
        import picamera
        camera = picamera.PiCamera()
        camera.start_preview()

    resolutions = {"high": (1280, 720), "medium": (640, 480), "low": (320, 240)}
    if args.resolution in resolutions:
        if args.use_usb:
            w, h = resolutions[args.resolution]
            camera.set(3, w)
            camera.set(4, h)
        else:
            camera.resolution = resolutions[args.resolution]
    else:
        raise Exception("%s not in resolution options." % args.resolution)

    main()