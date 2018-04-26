from __future__ import division

from collections import OrderedDict
import pprint as pp
import sys, os
sys.ps1 = 'SOMETHING'

import numpy as np

import matplotlib.pyplot as plt

#plt.gca().invert_yaxis()
plt.ion()

plotted_stuff = None

temp_write = 'lalala.log'


def plot_timechunks(time_chunks, words, delete_previous):

    #global plotted_stuff

    x = time_chunks.keys()
    x = np.array(x)
    #x = x / 10

    y = time_chunks.values()

    #print(x)
    #print(y)
    print(words)
    plt.plot(x, y, 'ro', markersize=0.25)

    if delete_previous:
        
        # only write to log when new diarization result arrives
        with open(temp_write, 'w') as f:
            for w in words:
                start = 'Speaker: ' + str(w.speaker).ljust(3)
                space = ' '.ljust(24 * w.speaker)
                f.write(start + space + str(w) + '\n')


    #for w in words:
        #print(w)
        #print(int(10 * w.start))
        #print(w.speaker)
        #plt.text(int(10 * w.start), w.speaker, w.w, rotation=90)


    plt.pause(0.001)
    #plt.show()
    #if delete_previous:
    #    del plotted_stuff

    #plotted_stuff = line
    #plt.draw()


class Word():

    def __init__(self, alignment_data, segment_start):
        self.w = alignment_data['word']
        self.start = alignment_data['start'] + segment_start
        self.end = alignment_data['length'] + self.start
        self.speaker = -1
        self.votes = []
        self.special = False
        self.proximity = 1

    # if know previous speaker, we can say, if votes are too close then just assign to
    # previous guy?
    def designate_speaker(self, time_chunks, previous_speaker):
        
        # to redesignate
        self.speaker = -1
        self.votes = []
        self.special = False
        self.proximity = 1

        try:
            speaker_votes = [0, 0, 0, 0, 0, 0, 0, 0, 0]

            start = int(self.start * 10)
            end = int(self.end * 10) + 1
            for i in range(start, end):
                speaker_votes[time_chunks[i]] += 1
            
            self.speaker = np.argsort(speaker_votes)[-1]
            self.votes = speaker_votes
            self.proximity = float(abs(self.votes[0] - self.votes[1])) / (sum(self.votes))

            if self.proximity < 0.25:
                self.speaker = previous_speaker
                self.special = True

            print(self)

            if self.speaker == -1:
                return False
            else:
                return True

        except Exception as e:
            print ('ERROR AT DESIGNATING SPEAKER!')
            print(time_chunks)
            print(e.message)
            return False

    def to_string(self):
        if self.special == True:
            word = self.w + '!'
        else:
            word = self.w
        return (word + ': (' + str(self.start) + ', '+ str(self.end) + ')' + \
                ' by ' + str(self.speaker) + ' votes: ' + str(self.votes) + \
                ' proximity ' + str(self.proximity))

    def __str__(self):
        return self.to_string()

    def __repr__(self):
        return self.to_string()


class ClientProcessor():

    def __init__(self):
        self.words = []
        self.diarization_intervals = []
        self.time_chunks = OrderedDict()

        self.diarized_word_index = 0


    def receive_message(self, message):
        
        #pp.pprint(message)

        if message['result']['final'] == True:
            print('final result')
            for w in message['result']['hypotheses'][0]['word-alignment']:
                self.words.append(Word(w, message['segment-start']))

            pp.pprint(self.words)


        if 'diarization' in message['result']:
            if len(message['result']['diarization']) > 0:
                if message['result']['diarization_incremental'] == True:
                    self.diarization_intervals.extend(message['result']['diarization'])
                    print (self.diarization_intervals[-1])
                    self.generate_time_chunks(True, message['result']['diarization'])
                    sys.stdout.flush()
                   
                else:
                    self.diarization_intervals = message['result']['diarization']
                    self.generate_time_chunks(False)
                    #self.compare_with_truth()


    def generate_time_chunks(self, append=True, data=None):
        
        print ('append is ' + str(append))

        if append:
            assert data is not None

            ending_speaker = list(self.time_chunks.values())[-1]

            for d in data:
                d_start = int(d['start'] * 10)
                d_end = int(d['end'] * 10) + 1
                
                print((d_start, d_end))

                # to fill in empty places
                existing_start = len(self.time_chunks)
                for i in range(existing_start, d_start):
                    self.time_chunks[i] = ending_speaker

                for i in range(d_start, d_end):
                    self.time_chunks[i] = d['speaker']

        else:
            self.time_chunks = OrderedDict()
            self.diarized_word_index = 0

            ending_speaker = 0

            for d in self.diarization_intervals:

                d_start = int(d['start'] * 10)
                d_end = int(d['end'] * 10)
                
                print((d_start, d_end))

                # to fill in empty places
                existing_start = len(self.time_chunks)
                for i in range(existing_start, d_start):
                    self.time_chunks[i] = ending_speaker


                for i in range(d_start, d_end):
                    self.time_chunks[i] = d['speaker']

        old_word_index = self.diarized_word_index

        modified = False
        prev_speaker = 0
        for i in range(self.diarized_word_index, len(self.words)):
            if self.words[i].designate_speaker(self.time_chunks, prev_speaker):
                prev_speaker = self.words[i].speaker
                pass
            else:
                self.diarized_word_index = i
                modified = True
                break
        
        if not modified:
            self.diarized_word_index = len(self.words)

        print('Plotting for word ' + str(old_word_index) + ' : ' + str(self.diarized_word_index))

        plot_timechunks(self.time_chunks, self.words[old_word_index:self.diarized_word_index], append == False)


    def print_diarization_result(self):
        pass