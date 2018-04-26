__author__ = 'tanel'

STATUS_EOS = -1
STATUS_SUCCESS = 0
STATUS_NO_SPEECH = 1
STATUS_ABORTED = 2
STATUS_AUDIO_CAPTURE = 3
STATUS_NETWORK = 4
STATUS_NOT_ALLOWED = 5
STATUS_SERVICE_NOT_ALLOWED = 6
STATUS_BAD_GRAMMAR = 7
STATUS_LANGUAGE_NOT_SUPPORTED = 8
STATUS_NOT_AVAILABLE = 9



def getuuid(length=6):
    shortuuid.set_alphabet('0123456789')
    val = shortuuid.uuid()
    return val[:length]

import shortuuid