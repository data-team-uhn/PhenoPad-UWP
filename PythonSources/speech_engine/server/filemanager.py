from os.path import basename
import os, sys, re
import logging
import time
from shutil import copyfile
import zipfile

ROOT_PATH = os.path.join(os.environ['HOME'], 'engine_storage')
VS_LOCAL = re.compile('note_[0-9]+')

#### Folders created according to visual studio
'''
var notebookFolder = await localFolder.GetFolderAsync(id);
var pageFolder = await notebookFolder.CreateFolderAsync(pageId.ToString(), CreationCollisionOption.OpenIfExists);
await pageFolder.CreateFolderAsync("Strokes", CreationCollisionOption.OpenIfExists);
await pageFolder.CreateFolderAsync("ImagesWithAnnotations", CreationCollisionOption.OpenIfExists);
await pageFolder.CreateFolderAsync("Phenotypes", CreationCollisionOption.OpenIfExists);
await pageFolder.CreateFolderAsync("Video", CreationCollisionOption.OpenIfExists);
await pageFolder.CreateFolderAsync("Audio", CreationCollisionOption.OpenIfExists);
'''
#######################################################



# Python 2.7 way of making directory
def _mkdir_recursive(path):
    sub_path = os.path.dirname(path)
    if not os.path.exists(sub_path):
        _mkdir_recursive(sub_path)
    if not os.path.exists(path):
        os.mkdir(path)

_mkdir_recursive(ROOT_PATH)


def prepare_directory(user=None, path=None):

    logging.info('Preparing directory for user: %s, path: %s' % (str(user), str(path)))

    folder_path = os.path.dirname(path)
    
    full_path = os.path.join(ROOT_PATH, str(user), folder_path)
    full_file_path = os.path.join(ROOT_PATH, str(user), path)
    _mkdir_recursive(full_path)

    return full_file_path


# Search through directory until finding 
def find_appropriate_path(full_path):
    if '\\' in full_path:
        folders = full_path.split('\\')
    elif '/' in full_path:
        folders = full_path.split('/')
    else:
        error_string = 'Invalid path %s' % full_path
        logging.error(error_string)
        return False, error_string

    start = -1
    for i, fd in enumerate(folders):
        # this should local folder created by Visual Studio
        if VS_LOCAL.search(fd):
            start = i
            break

    if start == -1:
        error_string = 'Unable to find appropriate "root" %s' % full_path
        logging.error(error_string)
        return False, error_string


    wanted_path = '/'.join(folders[start:])
    logging.info(folders)
    logging.info('Desired path is %s' % wanted_path)
    return True, wanted_path


def fm_get(user=None, credential=None, path=None):
    file_path = prepare_directory(user, path)

    logging.info('Reading content from path %s' %(file_path))
    f = open(file_path, 'rb')

    return f


def zipdir(path, zip):
    for root, dirs, files in os.walk(path):
        for file in files:

            splits = root.split('/')
            start = -1
            for i in range(len(splits)):
                if splits[i] == basename(path):
                    start = i + 1
                    break
            aname = os.path.join('/'.join(splits[start:]), file)
            logging.info(aname)
            zip.write(os.path.join(root, file), aname)


def fm_get_user_all(user):
    full_path = os.path.join(ROOT_PATH, str(user))
    output_file = os.path.join('/tmp', str(user) + '.zip')

    zipf = zipfile.ZipFile(output_file, 'w')
    zipdir(full_path, zipf)
    zipf.close()

    #zipf = zipfile.ZipFile(output_file, 'w', zipfile.ZIP_DEFLATED)
    #zipdir(full_path, zipf)
    #zipf.write(full_path, basename(full_path))
    #zipf.close()

    opened_file = open(output_file, 'rb')
    return opened_file


def fm_put(user=None, credential=None, path=None, content=None):
    file_path = prepare_directory(user, path)

    logging.info('Writing content size of %d to path %s' %(len(content), file_path))
    with open(file_path, 'wb') as f:
        f.write(content)


def fm_stream_put_prep(user=None):
    file_path = os.path.join('/tmp/user_' + str(user) + str(time.time()))

    f = open(file_path, 'wb')
    return f, file_path


def fm_stream_put(f=None, content=None):
    
    logging.info('Streaming content size of %d' %(len(content)))
    f.write(content)
    f.flush()


def fm_stream_put_finish(user=None, credential=None, path=None, temp_path=None):
    file_path = prepare_directory(user, path)
    copyfile(temp_path, file_path)


def fm_stream_put_finish_user_all(user, temp_path):
    file_path = prepare_directory(user, 'allnotes.zip')
    copyfile(temp_path, file_path)

    zip_ref = zipfile.ZipFile(file_path, 'r')
    zip_ref.extractall(os.path.dirname(file_path))
    zip_ref.close()

    os.remove(file_path)
    logging.info('temporary zip file ' + file_path + ' has been removed')