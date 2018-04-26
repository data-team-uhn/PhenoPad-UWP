#!/usr/bin/env python

"""Usage: python file_uploader.py [--put] file1.txt file2.png ...
Demonstrates uploading files to a server, without concurrency. It can either
POST a multipart-form-encoded request containing one or more files, or PUT a
single file without encoding.
See also file_receiver.py in this directory, a server that receives uploads.
"""

from __future__ import print_function
import mimetypes
import os
import sys
from functools import partial
from uuid import uuid4


try:
    from urllib.parse import quote
except ImportError:
    # Python 2.
    from urllib import quote

from tornado import gen, httpclient, ioloop
from tornado.options import define, options

SIZE_KB=1024
SIZE_MB=SIZE_KB*SIZE_KB
SIZE_GB=SIZE_KB*SIZE_KB*SIZE_KB
SIZE_TB=SIZE_KB*SIZE_KB*SIZE_KB*SIZE_KB


url = 'http://localhost:8888/file_manager'

# Using HTTP POST, upload one or more files in a single multipart-form-encoded
# request.
@gen.coroutine
def multipart_producer(boundary, filenames, write):
    boundary_bytes = boundary.encode()

    for filename in filenames:
        filename_bytes = filename.encode()
        mtype = mimetypes.guess_type(filename)[0] or 'application/octet-stream'
        buf = (
            (b'--%s\r\n' % boundary_bytes) +
            (b'Content-Disposition: form-data; name="%s"; filename="%s"\r\n' %
             (filename_bytes, filename_bytes)) +
            (b'Content-Type: %s\r\n' % mtype.encode()) +
            b'\r\n'
        )
        yield write(buf)
        with open(filename, 'rb') as f:
            while True:
                # 16k at a time.
                chunk = f.read(16 * 1024)
                if not chunk:
                    break
                yield write(chunk)

        yield write(b'\r\n')

    yield write(b'--%s--\r\n' % (boundary_bytes,))


# Using HTTP PUT, upload one raw file. This is preferred for large files since
# the server can stream the data instead of buffering it entirely in memory.
@gen.coroutine
def post(filenames):
    client = httpclient.AsyncHTTPClient()
    boundary = uuid4().hex
    headers = {'user-id': '12345', 'Content-Type': 'multipart/form-data; boundary=%s' % boundary}
    print(headers)
    producer = partial(multipart_producer, boundary, filenames)
    response = yield client.fetch(url + '/post',
                                  method='POST',
                                  headers=headers,
                                  body_producer=producer,
                                  request_timeout=6000.0)

    print(response)


@gen.coroutine
def raw_producer(filename, write):
    with open(filename, 'rb') as f:
        while True:
            # 512K at a time.
            chunk = f.read(512 * 1024)
            if not chunk:
                # Complete.
                break

            yield write(chunk)


@gen.coroutine
def put(filenames):
    client = httpclient.AsyncHTTPClient()
    for filename in filenames:
        mtype = mimetypes.guess_type(filename)[0] or 'application/octet-stream'
        headers = {'user-id': '12345', 'Content-Type': mtype}
        producer = partial(raw_producer, filename)
        #url_path = quote(os.path.basename(filename))
        url_path = quote(filename)
        response = yield client.fetch(url + '/put/%s' % url_path,
                                      method='PUT',
                                      headers=headers,
                                      body_producer=producer,
                                      request_timeout=6000.0)
        print(response)


@gen.coroutine
def get(filenames):
    httpclient.AsyncHTTPClient.configure(None, max_body_size=SIZE_GB)
    client = httpclient.AsyncHTTPClient()
    for filename in filenames:
        mtype = mimetypes.guess_type(filename)[0] or 'application/octet-stream'
        #headers = {'user-id': '12345', 'Content-Type': mtype}
        headers = {'user-id': '12345'}
        print(headers)
        #producer = partial(raw_producer, filename)
        #url_path = quote(os.path.basename(filename))
        url_path = quote(filename)
        print(url_path)
        print(url + '/get/%s' % url_path)
        response = yield client.fetch(url + '/get/%s' % url_path,
                                method='GET',
                                headers=headers,
                                request_timeout=6000.0)
        print(response)
        print(response.body)



def old_main():
    define("put", type=bool, help="Use PUT instead of POST", group="file uploader")

    # Tornado configures logging from command line opts and returns remaining args.
    filenames = options.parse_command_line()
    if not filenames:
        print("Provide a list of filenames to upload.", file=sys.stderr)
        sys.exit(1)

    method = put if options.put else post
    ioloop.IOLoop.current().run_sync(lambda: method(filenames), timeout=600)


def main():


    if len(sys.argv) == 2:
        global url
        url = 'http://' + sys.argv[1] + ':8888/file_manager'

    print('Using URL: ' + str(url))

    do_loop = True
    while do_loop:
        string = raw_input('(REST) Enter command: ')

        try:
            string = string.replace('~', os.environ['HOME'])
        except:
            pass
            
        splits = string.strip().split(' ')
        splits[0] = splits[0].lower()

        try:
            if 'quit' in splits:
                do_loop = False
            elif 'put' == splits[0]:
                ioloop.IOLoop.current().run_sync(lambda: put(splits[1:]), timeout=6000)
            elif 'get' == splits[0]:
                ioloop.IOLoop.current().run_sync(lambda: get(splits[1:]), timeout=6000)
            elif 'post' == splits[0]:
                ioloop.IOLoop.current().run_sync(lambda: post(splits[1:]), timeout=6000)
            else:
                print('Command ' + str(string) + ' is not well-formed or not allowed')
        except:
            print('Mysteriously failed, try again?')
               
if __name__ == "__main__":
    main()