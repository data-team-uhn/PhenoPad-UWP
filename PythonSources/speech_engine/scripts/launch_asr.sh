LOG_DIR="$HOME/engine_log/" 

SCRIPT_DIR="../scripts/"
CONFIG_DIR="../configs/"
CLIENT_DIR="../client/"
SERVER_DIR="../server/"

#config_file=${CONFIG_DIR}/use_pubmed.yaml
config_file=${CONFIG_DIR}/use_nnet3.yaml
manager_id=$1

export GST_PLUGIN_PATH=${HOME}"/gst-kaldi-nnet2-online/src"

echo "Launching ASR worker"

timestamp=$(date +%Y-%m-%d_%H-%M-%S)
command="python ${SERVER_DIR}/worker_asr.py -u ws://localhost:8888/asr -c ${config_file} -m $manager_id 2>&1 | tee ${LOG_DIR}/asr_$timestamp.log &"
echo "Running $command"
eval $command

if [ $? -eq 0 ]; then
    echo "ASR worker launched"
else
    echo "ERROR on launching ASR worker"
fi
