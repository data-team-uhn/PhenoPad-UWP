import atexit
import os, time, sys

from filelock import FileLock

WORKER_COUNTER_FILE = os.path.join('/tmp/worker_counter.log')
WORKER_COUNTER_LOCK = os.path.join('/tmp/worker_counter.lock')

lock = FileLock(WORKER_COUNTER_LOCK)

WORKER_CAP = 2

rest_server_process = None

def on_start_up():

    lock.acquire()
    # proceed to write
    total = check_existing()
    total += 1
    write_to_file(total)

    lock.release()


def on_exit():

    lock.acquire()
    # proceed to write
    total = check_existing()
    total = max(total - 1, 0)
    write_to_file(total)

    lock.release()


def initialize():
    lock.acquire()
    write_to_file(0)
    lock.release()


def clean_up():
    lock.acquire()
    write_to_file(0)
    lock.release()

    # hopefully this actually terminates the rest server :D
    rest_server_process.terminate()


# no lock, acquire from outside of this function
def check_existing():
    total = 0
    if os.path.exists(WORKER_COUNTER_FILE):
        with open(WORKER_COUNTER_FILE, 'r') as f:
            temp = f.readlines()[0]
            total = int(temp.strip())
    
    print('Current counter is ' + str(total))
    return total


def check_existing_and_exit():
    lock.acquire()
    total = check_existing()

    if total > WORKER_CAP:
        lock.release()
        sys.exit('Too many workers, exiting....')
    lock.release()


# no lock, acquire from outside of this function
def write_to_file(val):
    with open(WORKER_COUNTER_FILE, 'w') as f:
        f.write(str(val) + '\n')


def register_exit_action():
    atexit.register(on_exit)


def register_initialize_on_exit(rest_process):
    global rest_server_process
    rest_server_process = rest_process

    atexit.register(clean_up)


if __name__ == '__main__':
    on_start_up()
    register_exit_action()

    counter = 1
    while counter <= 10:
        time.sleep(0.25)
        counter += 1
    
    sys.exit('Exiting...')
