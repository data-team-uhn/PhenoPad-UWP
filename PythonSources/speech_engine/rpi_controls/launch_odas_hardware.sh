HOME="/home/pi/"

RPI_PATH=$HOME/speech_engine/rpi_controls/
ODAS_PATH=$HOME/speech_engine/odas/

LOG_DIR="$HOME/engine_log/" 

SCRIPT_DIR="$HOME/speech_engine/scripts/"
CONFIG_DIR="$HOME/speech_engine/configs/"
CLIENT_DIR="$HOME/speech_engine/client/"
SERVER_DIR="$HOME/speech_engine/server/"

mkdir -p $LOG_DIR

#config_file=${CONFIG_DIR}/use_pubmed.yaml
manager_id=$1
server_ip=$2

echo "Launching ODAS hardware"

timestamp=$(date +%Y-%m-%d_%H-%M-%S)
command="/${ODAS_PATH}/bin/odascore -c ${ODAS_PATH}/config/respeaker.cfg 2>&1 | tee ${LOG_DIR}/odas_hardware_$timestamp.log &"
echo "Running $command"
eval $command

PID=$!

echo "PID=$PID"
echo "$PID" > /tmp/odas_hardware_pid

echo "ODAS hardware launched"