HOME="/home/pi/"

RPI_PATH=$HOME/speech_engine/rpi_controls/

LOG_DIR="$HOME/engine_log/"

SCRIPT_DIR="$HOME/speech_engine/scripts/"
CONFIG_DIR="$HOME/speech_engine/configs/"
CLIENT_DIR="$HOME/speech_engine/client/"
SERVER_DIR="$HOME/speech_engine/server/"

mkdir -p $LOG_DIR

#config_file=${CONFIG_DIR}/use_pubmed.yaml
manager_id=$1
server_ip=$2

echo "Launching ODAS audio controller"

timestamp=$(date +%Y-%m-%d_%H-%M-%S)
command="python3 ${RPI_PATH}/odas_audio_control.py -i $server_ip -m $manager_id 2>&1 | tee ${LOG_DIR}/odas_audio_$timestamp.log &"
echo "Running $command"
eval $command

PID=$!

echo "PID=$PID"
echo "$PID" > /tmp/odas_service_pid

echo "odas audio controller launched"


echo "Launching ODAS track controller"

timestamp=$(date +%Y-%m-%d_%H-%M-%S)
command="python3 ${RPI_PATH}/odas_track_control.py -p $server_ip -m $manager_id 2>&1 | tee ${LOG_DIR}/odas_track_$timestamp.log &"
echo "Running $command"
eval $command

PID=$!

echo "PID=$PID"
echo "$PID" >> /tmp/odas_service_pid

echo "ODAS track controller launched"

