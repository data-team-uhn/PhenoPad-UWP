LOG_DIR="$HOME/engine_log/" 

SCRIPT_DIR="../scripts/"
CONFIG_DIR="../configs/"
CLIENT_DIR="../client/"
SERVER_DIR="../server/"

#config_file=${CONFIG_DIR}/use_pubmed.yaml
manager_id=$1

echo "Launching ODAS worker"

timestamp=$(date +%Y-%m-%d_%H-%M-%S)
command="python ${SERVER_DIR}/worker_odas.py -u ws://localhost:8888/odas -m $manager_id 2>&1 | tee ${LOG_DIR}/odas_$timestamp.log &"
echo "Running $command"
eval $command

if [ $? -eq 0 ]; then
    echo "ODAS worker launched"
else
    echo "ERROR on launching ODAS worker"
fi
