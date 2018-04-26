LOG_DIR="$HOME/engine_log/" 

SCRIPT_DIR="../scripts/"
CONFIG_DIR="../configs/"
CLIENT_DIR="../client/"
SERVER_DIR="../server/"

if [ "TRUE" = "TRUE" ]; then
    rm -rf $LOG_DIR/*
fi

mkdir -p $LOG_DIR

killall python

echo -e "\n\n-------\n"

# 1. Launch REST server
timestamp=$(date +%Y-%m-%d_%H-%M-%S)
command="bash launch_rest.sh 2>&1 | tee ${LOG_DIR}/go_rest_$timestamp.log &"
echo "Running $command"
eval $command

sleep 2

# 2. Launch Python tornado server
timestamp=$(date +%Y-%m-%d_%H-%M-%S)
command="bash launch_server.sh 2>&1 | tee ${LOG_DIR}/go_tornado_$timestamp.log &"
echo "Running $command"
eval $command