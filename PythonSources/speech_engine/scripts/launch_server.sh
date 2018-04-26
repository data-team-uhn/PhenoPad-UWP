LOG_DIR="$HOME/engine_log/" 

SCRIPT_DIR="../scripts/"
CONFIG_DIR="../configs/"
CLIENT_DIR="../client/"
SERVER_DIR="../server/"

echo "Launching tornado server"

timestamp=$(date +%Y-%m-%d_%H-%M-%S)
command="python ${SERVER_DIR}/server_tornado.py 2>&1 | tee ${LOG_DIR}/tornado_$timestamp.log & "
echo "Running $command"
eval $command

if [ $? -eq 0 ]; then
    echo "Tornado server launched"
else
    echo "ERROR on launching tornado server"
fi