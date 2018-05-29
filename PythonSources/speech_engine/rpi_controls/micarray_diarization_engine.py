from __future__ import division

#import pyaudio
import numpy as np
import pprint as pp
import yaml
import wave
import logging
#import pyannote.core
#from pyannote.core.segment import Segment
#from sklearn.mixture import GaussianMixture
#from sklearn.cluster import KMeans
#from sklearn.cluster import MeanShift
import scipy.io.wavfile
import collections

#from feature_extraction.yaafe import YaafeMFCC
#from feature_extraction.feature import SlidingWindowFeature
from entity.Track import Track

import struct
import math
import array
import json
#FIXME
#import rest_server_side_client as restServer

import time, os, sys, copy, ast
from multiprocessing import Process, Lock, Manager, Queue
import webrtcvad
vad = webrtcvad.Vad(1)

BYTES_PER_FRAME = 2     # Number of bytes in each audio frame. Int 16
CHANNELS = 1            # Number of channels.
SAMPLING_RATE = 16000   # Audio sampling rate.

RESULT_TYPE = ["startspeaking", "incremental", "retrain"]

#experiment_dir = os.path.join(os.environ['HOME'], "pyannote-audio/tutorials/speaker-embedding")
experiment_dir = "feature_extraction"

#model_dir = os.path.join(os.environ['HOME'], '/pyannote-audio/models/0.5+0.1/0096.h5')
model_dir = os.path.join(os.environ['HOME'], '/pyannote-audio/models/2+0.5/0070.h5')

logger = logging.getLogger(__name__)
logger.propagate = False

logging.basicConfig(filename='diarization.log', level=logging.DEBUG, format="%(levelname)8s %(asctime)s %(message)s ")
logging.debug('Starting up diarization engine')

from enum import Enum


def unit_vector(vector):
    """ Returns the unit vector of the vector.  """
    return vector / np.linalg.norm(vector)

def angle(v1, v2):
    u_v1 = unit_vector(v1)
    u_v2 = unit_vector(v2)
    return np.arccos(np.clip(np.dot(v1, v2), -1.0, 1.0))

def angle_degree(v1, v2):
    u_v1 = unit_vector(v1)
    u_v2 = unit_vector(v2)
    return np.degrees(np.arccos(np.clip(np.dot(v1, v2), -1.0, 1.0)))

class DiarizationMode(Enum):
    Direction = 1
    Feature = 2
    Both = 3

class Speaker:
    def __init__(self, sid):
        self.id = sid
        self.track_ids = []
        self.vector = None
        self.ring_buffer = collections.deque(maxlen=20)

    def add_vector(self, vector):
        self.ring_buffer.append(vector)
        self.vector = np.mean(np.array(self.ring_buffer), axis=0)

    def get_angle(self):
        if self.vector:
            return math.degrees(math.atan2(self.vector[1], self.vector[0]))
        return None

class MicarrayDiarizationEngine():
    def __init__ (self, track_queue, audio_file, result_queue, status_queue, worker_pid):

        logger.info('Preparing Diarization Engine at PID ' + str(os.getpid()))
        logger.info('Diarization Engine Master Worker at PID ' + str(worker_pid))
        self.worker_pid = worker_pid

        self.track_queue = track_queue
        self.audio_file = audio_file
        self.result_queue = result_queue
        self.status_queue = status_queue

        # self.engine_initial_training = True
        self.num_speakers = 2
        self.num_track = 4
        self.track_id = 0
        self.sample_rate = 16000
        self.bytes_per_frame = 2
        self.num_channel = 4
        self.bytes_per_second = self.num_channel * self.bytes_per_frame * self.sample_rate
        self.ang_dif_thred = 30 * math.pi / 180
        self.speakers = []
        self.tracks = {}
        for i in range(self.num_speakers):
            speaker = Speaker(i)
            self.speakers.append(speaker)

        #self.result_file = open('diarization_result.txt', 'w')
        
        # FIXME
        #restServer.create_worker(self.worker_pid, num_speakers=self.num_speakers)

        logger.info('Diarization engine initializing')
        """ diarization by embeddings """
        """
        self.total_dequeued = 0
        self.total_duration = 0
        self.embedding_has_audio = []
        self.emb_list = []
        self.stacked_features = None

        # load configuration file
        config_yml = experiment_dir + '/config.yml'
        with open(config_yml, 'r') as fp:
             config = yaml.load(fp)
             FeatureExtraction = YaafeMFCC
             self.feature_extraction = FeatureExtraction(
                     **config['feature_extraction'].get('params', {}))

        model_yaml = os.path.join(os.environ['HOME'], "pyannote-audio/models/2+0.5/config.yml")
        weights_file = os.path.join(os.environ['HOME'], "pyannote-audio/models/2+0.5/0070.h5")
        with open(model_yaml, 'r') as fp:
            config = yaml.load(fp)
            # architecture
            if 'architecture' in config:
                architecture_name = config['architecture']['name']
                models = __import__('pyannote.audio.embedding.models',
                        fromlist=[architecture_name])
                Architecture = getattr(models, architecture_name)
                architecture = Architecture(
                        **config['architecture'].get('params', {}))

            # approach
            if 'approach' in config:
                approach_name = config['approach']['name']
                approaches = __import__('pyannote.audio.embedding.approaches',
                        fromlist=[approach_name])
                Approach = getattr(approaches, approach_name)
                approach = Approach(
                        **config['approach'].get('params', {}))

            self.model = architecture((100, 35)) # 2s / 0.02s = 100
            self.model.load_weights(weights_file)

        self.initialize_clustering_method()
        self.all_embeddings = np.array([])
        self.retraining_counter = 0
        """
        logger.info('Diarization engine initialized')


    def initialize_clustering_method(self):
        #self.kmeans = KMeans(n_clusters=self.num_speakers, random_state=0)
        self.clustering_model = GaussianMixture(n_components=self.num_speakers)
        self.done_clustering =  False

    def cleanup(self):
        # clean up local reference to queues to not confuse
        # python garbage collector

        self.track_queue.close()
        self.audio_queue.close()
        self.result_queue.close()
        self.status_queue.close()

        del self.track_queue
        del self.status_queue
        del self.result_queue
        del self.audio_queue

        logger.warning('Exiting diarization process by brute force at PID ' + str(os.getpid()))
        sys.exit()


    def check_worker_status(self):

        stuff = None
        try:
            stuff = self.status_queue.get(False)
        except:
            e = sys.exc_info()
            #logger.warning('Status queue is empty ' + str(e))
            # nothing to except
            pass

        if stuff == 'CLOSED':
            return False
        else:
            return True

    # diarization stuff, only directional information is used for now
    def start_diarization(self):
        trackinfo_log = open("track_info.log", 'w')

        while self.check_worker_status():
            try:
                logger.debug('Diarization engine trying to fetch track info')
                dequeued_tracks = self.track_queue.get(True)
            except:
                time.sleep(1)
                logger.warning('Diarization engine cannot fetch track info, wait for 1 second')
                continue

            timespans = []
            for track_info in dequeued_tracks:
                # track_info is a Track (track state 0(initial state), 1 got mean vector, 2(end state) )
                # initial state: track just begins
                # end state: track just ends
                track_state = track_info.state
                track_id = track_info.id

                # write to file
                trackinfo_log.write(str(track_info) + "\n")

                if track_state == 0:
                    self.tracks[track_id] = track_info
                    #which_speaker = self.assign_speaker_by_direction(track_angle)

                    #track_info['speaker'] = which_speaker
                    #self.tracks.append(track_info)
                    #self.speakers[which_speaker]['angle'] = track_angle 
                    """
                    print("Speaker: {}, angle: {}, start: {}".format( \
                            track_info['speaker'], \
                            track_info['angle'], \
                            track_info['start']))
                    """
                elif track_state == 1:
                    self.tracks[track_id]  = track_info
                    self.diarization_by_direction(track_id)

                elif track_state == 2:
                    which_speaker = self.tracks[track_id].speaker
                    tk = self.tracks[track_id]
                    tk = track_info
                    tk.speaker = which_speaker
                    #print("Speaker {} stops".format(tk['speaker']))
                    #self.speakers[tk['speaker']]['angle'] = track_angle 
                    #print("Speaker: {}, angle: {}, start: {}, end: {}".format(tk['speaker'], tk['angle'], tk['start'], tk['end']))
                    #self.result_file.write(json.dumps(tk) + '\r\n')
                    self.append_result(
                            tk.speaker, \
                            tk.start, \
                            tk.end, \
                            tk.get_angle(), \
                            RESULT_TYPE[1]
                            )

                    #if track_info['end'] - track_info['start'] > 2:
                    """use this track to train model"""
                        #logger.debug("use long seg to train model, hasn't implemented")
        self.cleanup()

    def diarization_by_direction(self, track_id):
        track = self.tracks[track_id]
        which_speaker = self.assign_speaker_by_direction(track.mean_vector)
        track.speaker = which_speaker
        self.speakers[which_speaker].add_vector(track.get_mean_vector())
        self.append_result(
                which_speaker, \
                track.start, \
                -1, \
                track.get_angle(), \
                RESULT_TYPE[0]
                )



    def append_result(self, speaker, start, end, angle, result_type):
        timespans = []
        timespans.append(self.format_timespan( \
                speaker, \
                start, \
                end, \
                angle))
        self.result_queue.put((timespans, result_type))

    def assign_speaker_by_direction(self, vector):
        min_ind = -1
        min_temp = 1000.0
        empty_speaker = -1
        # find the speaker whose angle difference with this track is less than 15 degrees
        for speaker in self.speakers:
            spk_ind = speaker.id
            if not speaker.vector is None:
                ang_dif = angle(vector, speaker.vector)
                if ang_dif < min_temp:
                    min_temp = ang_dif
                    min_ind = spk_ind
                if ang_dif < self.ang_dif_thred:
                    return spk_ind
            elif empty_speaker == -1:
                empty_speaker = spk_ind
        # if no speaker close enough is found, assign the closest speaker to this track
        if empty_speaker != -1:
            return empty_speaker
        else:
            return min_ind

    def format_timespan(self, speaker, start, end, angle):
        span = {'speaker': speaker, 'start': start, 'end': end, 'angle' : angle}
        return span

    # Merge multiple channels into single one by taking the average
    def average_channels_to_one(self, n_channel,  all_bytes):
        if len(all_bytes) % (BYTES_PER_FRAME * n_channel) != 0:
            logger.warning("Need handling remaining bytes")
        audio_ints = array.array('h', all_bytes) # 16 bit PCM
        np_audio_ints = np.array(audio_ints, dtype=np.int16)
        merged_ints = np.mean(np_audio_ints.reshape(-1, n_channel), axis=-1).astype(np.int16) # take average every n_channel samples
        merged_ints = merged_ints.tolist()

        # pack and return, TODO how to average bytes directly
        return struct.pack('<%dh'%len(merged_ints), *merged_ints)


        

    """ Dirazrion using embeddings, ignore for now """

    # truncate to nice length for feature extraction
    # block_size = 512 samples (0.032s), step_size = 320 samples (0.02s)
    # ensure each block has 512 samples, 
    def diarization_chunk_to_nice_length(self, all_bytes):
        block_size = self.feature_extraction.block_size
        step_size = self.feature_extraction.step_size

        left_num = (len(all_bytes) // BYTES_PER_FRAME)  % step_size
        num_block = len(all_bytes) // BYTES_PER_FRAME  // step_size
        if left_num < block_size - step_size:
            """ not enough bytes for last block """
            num_block -= 1
            left_num = step_size + left_num

        desire_sample_num = num_block * step_size + block_size - step_size

        desire_bytes = all_bytes[:desire_sample_num * BYTES_PER_FRAME]
        leftover_bytes = all_bytes[-left_num * BYTES_PER_FRAME:]
        return desire_bytes, leftover_bytes


    def diarization_embedding(self, start, end):
        length = self.stacked_features.sliding_window.step * \
                self.stacked_features.data.shape[0]
        temp_list = []
        for timestamp in np.arange(start, end, 0.1):
            seg = np.array(self.stacked_features.crop(\
                    Segment(timestamp-1, timestamp+1), mode="center", fixed=2))
            emb = self.model.predict(seg[np.newaxis,:])
            temp_list.extend(emb)
        self.emb_list.extend(temp_list)

    def diarization_clustering(self):
        if len(self.emb_list) < 20:
            print("Not enough embeddings to fit, will not preceed")
            return

        self.clustering_model.fit(np.array(self.emb_list))

            
    def predict_and_print(self):
        predictions = self.clustering_model.predict(np.array(self.emb_list))
        started = False
        index = 0
        start = -1
        end = -1
        label = -1

        for pred in predictions:
            if pred != label:
                end = index - 1
                if end != -1:
                    print("{}: {}~{}".format(label, 0.1*start, 0.1*end))

                start = index
                if pred == 1:
                    label = pred
                elif pred == 0:
                    label = pred
            index += 1

    # Testing with input bytes, do something each time reviece a 2 second segment
    def test_diarization(self, input_bytes, leftover_bytes):
        # technically we do not need logger when testing
        logger.info('=== Diarization engine working ===')

        input_bytes = leftover_bytes + input_bytes
        

        #print('======= Merge Channels ======')
        #bytes_to_process = self.average_channels_to_one(2, input_bytes)
        #with open("converted.raw", "wb") as input_file: input_file.write(bytes_to_process)

        print('======= Chunk to nice length ======')
        desire_bytes, leftover_bytes = self.diarization_chunk_to_nice_length(input_bytes)
        raw_audio = np.frombuffer(desire_bytes, dtype=np.int16)

        self.total_dequeued += len(desire_bytes)
        start_time = self.total_duration
        self.total_duration += len(desire_bytes) / BYTES_PER_FRAME / SAMPLING_RATE
        
        print('=== Engine processed {}~{} seconds ==='.format(\
                start_time,
                self.total_duration))

        #print('======= Format Conversion ======')
        #bytes_to_process = self.diarization_format_convert(desired_bytes)

        #cleaned_audio_bytes = bytearray(list(bytes_to_process))
        #no_audio_starts = []
        print('======= VAD ======')
        #cleaned_audio_bytes, no_audio_starts = self.diarization_vad(bytes_to_process)

        print('======= Feature Extraction ======')
        features = self.feature_extraction.extract_from_bytes(raw_audio, SAMPLING_RATE)
        if self.stacked_features is None:
            self.stacked_features = features
        else:
            self.stacked_features.append_data(features.data)

        print('======= Embedding ======')
        self.diarization_embedding(start_time, self.total_duration)
        #self.diarization_embedding(0, self.total_duration)

        # We redo some diarization with previous chunk to ensure quality
        print('======= Clustering ======')
        self.diarization_clustering()

        print("======= Predicting ======")
        self.predict_and_print()
        return leftover_bytes
    


def start_micarray_diarization_engine(track_queue, audio_file, result_queue, status_queue, worker_pid):
    engine = MicarrayDiarizationEngine(track_queue, audio_file, result_queue, status_queue, worker_pid)
    engine.start_diarization()

def TEST_MERGE_CHANNEL(filename):
    engine = MicarrayDiarizationEngine(None, None, None, None, None)
    num_bytes = 4 * 2 * 16000 * 1 # 1 second
    with open(filename, 'rb') as raw_file:
        merged = engine.average_channels_to_one(4, raw_file.read(num_bytes))
        print(len(merged))
        print(type(merged))

def TEST_AUDIO(filename):
    import scipy.io.wavfile
    sample_rate, raw_audio = scipy.io.wavfile.read(filename)
    raw_bytes = raw_audio.tobytes()
    engine = MicarrayDiarizationEngine(None, None, None, None, None)

    num_bytes_each = 2 * 16000 * 60 # 606060606060606060606060606060606060606060606060606060606060606060606060606060606060606060606060606060606060606060606060 second
    leftover_bytes = b''
    #for i in range(0, len(raw_bytes), num_bytes_each):
    i = 0
    leftover_bytes = engine.test_diarization(raw_bytes[i:i+num_bytes_each], leftover_bytes)


if __name__ == '__main__':
    #TEST_AUDIO(sys.argv[1])
    TEST_MERGE_CHANNEL(sys.argv[1])
