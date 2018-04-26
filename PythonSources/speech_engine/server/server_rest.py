# Adapted from https://gist.github.com/miguelgrinberg/5614326

from flask import Flask, jsonify, abort, request, make_response, url_for
from flask.ext.httpauth import HTTPBasicAuth
from filelock import FileLock

import atexit
import cPickle as pickle
import os
import logging
from multiprocessing import Process
import thread
import pprint as pp

app = Flask(__name__, static_url_path = "")

MANAGER_INFO_FILE = os.path.join('/tmp/manager_info.log')
MANAGER_INFO_LOCK = os.path.join('/tmp/manager_info.lock')

lock = None
    
'''
connection_info = [
    {
        'manager_pid':          10000,
        'asr_pid':              20000,
        'diarization_pid':      30000,
        'num_speakers': 2,
        'full_diarization_timestamp': 0
    },
    {
        # another manager info....
    }
]
'''

manager_info = []

default_info = {
        'manager_pid':                  0,
        'file_manager_pid':             0,
        'asr_pid':                      0,
        'diarization_pid':              0,
        'odas_pid':                     0,
        'ui_pid':                       0,
        'audio_pid':                    0,
        'tracking_pid':                 0,
        'num_speakers':                 1,
        'full_diarization_timestamp':   0
    }


@app.errorhandler(400)
def not_found(error):
    return make_response(jsonify( { 'error': 'Bad request' } ), 400)


@app.errorhandler(404)
def not_found(error):
    return make_response(jsonify( { 'error': 'Not found' } ), 404)


def get_manager_info():
    global manager_info

    lock.acquire()

    try:
        with open(MANAGER_INFO_FILE, 'r') as f:
            manager_info = pickle.load(f)
    except:
        manager_info = []

    # We do not want to repeately create the same manager :D
    repeated = False
    for i, w in enumerate(manager_info):
        if w['manager_pid'] == default_info['manager_pid']:
            default_info[i] = default_info
            repeated = True
            break
    
    if not repeated:
        manager_info.append(default_info)

    lock.release()


def persist_manager_info():
    lock.acquire()
    with open(MANAGER_INFO_FILE, 'w') as f:
        pickle.dump(manager_info, f)

    lock.release()


def make_public_info(info):
    new_task = {}
    for field in info:
        if field == 'manager_pid':
            new_task['uri'] = url_for('get_info', manager_pid=info['manager_pid'], _external=True)
            new_task['manager_pid'] = info[field]
        else:
            new_task[field] = info[field]
    #logging.info("New Task: ")
    #logging.info(new_task)
    return new_task
    

@app.route('/config/api/manager_info', methods = ['GET'])
def get_info_all():
    get_manager_info()
    logging.info(manager_info)
    return jsonify( { 'info': map(make_public_info, manager_info) } )


@app.route('/config/api/manager_info/<string:manager_pid>', methods = ['GET'])
def get_info(manager_pid):
    get_manager_info()
    manager_pid = str(manager_pid)

    info = filter(lambda t: t['manager_pid'] == str(manager_pid), manager_info)
    if len(info) == 0:
        abort(404)
    
    persist_manager_info()

    logging.info("Getting manager" + str(manager_pid))

    return jsonify( { 'info': make_public_info(info[0]) } )


@app.route('/config/api/manager_info', methods = ['POST'])
def create_info():
    get_manager_info()
    
    if not request.json:
        abort(400)

    info = {
        'manager_pid':                  request.json['manager_pid'],
        'file_manager_pid':             0,
        'asr_pid':                      0,
        'diarization_pid':              0,
        'odas_pid':                     0,
        'ui_pid':                       0,
        'audio_pid':                    0,
        'tracking_pid':                 0,
        'num_speakers':                 1,
        'full_diarization_timestamp':   0
    }

    for k in info:
        if k in request.json:
            info[k] = request.json[k]

    # We do not want to repeately create the same manager :D
    repeated = False
    for i, w in enumerate(manager_info):
        if w['manager_pid'] == request.json['manager_pid']:
            manager_info[i] = info
            repeated = True
            break
    
    if not repeated:
        manager_info.append(info)

    persist_manager_info()
    return jsonify( { 'info': make_public_info(info) } ), 201


@app.route('/config/api/manager_info/<string:manager_pid>', methods = ['DELETE'])
def delete_info(manager_pid):
    get_manager_info()
    manager_pid = str(manager_pid)

    result = False
    if manager_pid != 0:
        info = filter(lambda t: t['manager_pid'] == str(manager_pid), manager_info)
        if len(info) == 0:
            abort(404)
        manager_info.remove(info[0])
        persist_manager_info()
    else:
        logging.info('User not allowed to delete manager_pid:0')
    return jsonify( { 'result': result } )


@app.route('/config/api/manager_info/<string:manager_pid>', methods = ['PUT'])
def update_info(manager_pid):
    get_manager_info()

    logging.info('==> Searching for ' + str(manager_pid))
    logging.info('Current managers are ' + str(manager_info))
    logging.info('Request JSON is ' + str(request.json))

    manager_pid = str(manager_pid)

    info = filter(lambda t: t['manager_pid'] == str(manager_pid), manager_info)
    #logging.info(request.json)
    if len(info) == 0:
        abort(404)
    if not request.json:
        abort(400)

    for k in default_info:
        #logging.info('Looking for ' + str(k) + ' ' + str(k in request.json))
        if k in request.json:
            new_val = request.json.get(k, info[0][k])
            old_val = info[0][k]
            info[0][k] = new_val
            logging.info("Updating manager(%s) for %s from %s to %s" % \
                    (info[0]['manager_pid'], k, str(old_val), str(new_val)))

    persist_manager_info()
    return jsonify( { 'info': make_public_info(info[0]) } )


def launch_rest_server():
    app.run(debug = True, host='0.0.0.0')


if __name__ == '__main__':
    global lock

    try:
        os.remove(MANAGER_INFO_FILE)
    except:
        pass
    
    try:
        os.remove(MANAGER_INFO_LOCK)
    except:
        pass
    

    lock = FileLock(MANAGER_INFO_LOCK)

    logging.basicConfig(level=logging.INFO, format="%(levelname)8s %(asctime)s %(message)s ")
    logging.info('Rest server is starting')

    launch_rest_server()