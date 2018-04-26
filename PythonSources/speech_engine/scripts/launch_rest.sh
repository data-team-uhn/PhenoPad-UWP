LOG_DIR="$HOME/engine_log/" 

SCRIPT_DIR="../scripts/"
CONFIG_DIR="../configs/"
CLIENT_DIR="../client/"
SERVER_DIR="../server/"

echo "Launching REST server"

timestamp=$(date +%Y-%m-%d_%H-%M-%S)
command="python ${SERVER_DIR}/server_rest.py 2>&1 | tee ${LOG_DIR}/rest_$timestamp.log &"
echo "Running $command"
eval $command

if [ $? -eq 0 ]; then
    echo "REST server launched"
else
    echo "ERROR on launching REST server"
fi
