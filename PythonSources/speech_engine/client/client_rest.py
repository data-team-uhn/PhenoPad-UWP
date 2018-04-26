import requests, json, yaml
import sys
import pprint as pp

#host_url = "http://54.226.217.30:5000/config/api/manager_info"
#host_url = "http://localhost:5000/config/api/manager_info"
sickkids_ip = "speechengine.ccm.sickkids.ca"
local_ip = "localhost"

host_url = "NONE"

def get_host_url(host_ip):
    global host_url
    host_url = "http://" + host_ip + ":5000/config/api/manager_info"

print('Initializing REST server with localhost first :D')
get_host_url(local_ip)

def create_manager(pid, parameters):

    url = host_url

    manager = {'manager_pid': pid}
    for p in parameters:
        manager[p] = parameters[p]
    data = json.dumps(manager)

    headers = {"Content-Type": "application/json"}
    response = requests.post(url, data=data, headers=headers)
    #print(response.text)
    return yaml.safe_load(response.text)


def update_manager(pid, parameters={}):
    url = host_url + '/' + str(pid)
    
    manager = {}
    for p in parameters:
        manager[p] = parameters[p]

    data = json.dumps(manager)

    headers = {"Content-Type": "application/json"}
    response = requests.put(url, data=data, headers=headers)
    #print(response.text)
    return yaml.safe_load(response.text)


def get_manager(pid):
    url = host_url + '/' + str(pid)
    response = requests.get(url)
    #print(response.text)
    manager = yaml.safe_load(response.text)
    return manager


def get_all_managers():
    url = host_url
    response = requests.get(url)
    #print(response.text)
    managers = yaml.safe_load(response.text)
    return managers


def delete_manager(pid):
    url = host_url + '/' + str(pid)
    response = requests.delete(url)
    #print(response.text)
    return yaml.safe_load(response.text)



def rest_command_line():
    do_loop = True
    while do_loop:
        string = raw_input('(REST) Enter command: ')
        splits = string.strip().split(' ')
        for i, s in enumerate(splits):
            splits[i] = s.lower()

        if 'quit' in splits:
            do_loop = False
        elif splits[0] == 'get' and len(splits) == 1:
            managers = get_all_managers()
            pp.pprint(managers)

        elif splits[0] == 'get' and len(splits) == 2:
            manager = get_manager(int(splits[1]))
            print(manager)

        elif splits[0] == 'delete' and len(splits) == 2:
            result = delete_manager(int(splits[1]))
            print(result)

        elif splits[0] == 'update' and len(splits) % 2 == 0:
            parameters = {}
            for i in range(2, len(splits), 2):
                parameters[splits[i]] = int(splits[i + 1])
            result = update_manager(int(splits[1]), parameters)
            print(result)

        elif splits[0] == 'create' and len(splits) % 2 == 0:
            parameters = {}
            for i in range(2, len(splits), 2):
                parameters[splits[i]] = int(splits[i + 1])
            result = create_manager(int(splits[1]), parameters)
            print(result)



if __name__ == '__main__':
    '''
    create_manager(29999, 2, 1000)
    manager = get_manager(29999)
    print(manager['info']['manager_pid'])
    update_manager(29999, None, 2999)
    delete_manager(29999)
    '''
    #get_manager(1000)
    #update_manager(int(sys.argv[1]), int(sys.argv[2]), 2999)

    if len(sys.argv) == 1:
        print('Default to localhost')
        address = 'localhost'
    else:
        address = sys.argv[1]

    get_host_url(address)

    print('Using URL %s' % (str(host_url)))

    rest_command_line()