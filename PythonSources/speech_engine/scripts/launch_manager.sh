LOG_DIR="$HOME/engine_log/" 

SCRIPT_DIR="../scripts/"
CONFIG_DIR="../configs/"
CLIENT_DIR="../client/"
SERVER_DIR="../server/"

mkdir -p $LOG_DIR

initiating_ui_id=$1

echo "Launching worker manager"

timestamp=$(date +%Y-%m-%d_%H-%M-%S)
command="python ${SERVER_DIR}/worker_manager.py -c ${initiating_ui_id} 2>&1 | tee ${LOG_DIR}/manager_$timestamp.log &"
echo "Running $command"
eval $command

if [ $? -eq 0 ]; then
    echo "Worker manager launched"
else
    echo "ERROR on launching worker manager server"
fi