killall python

RPI_PATH=$HOME/speech_engine/rpi_controls/

command="python ${RPI_PATH}/server.py &"
echo $command
eval $command

sleep 3

command="sudo python ${RPI_PATH}/controlClent.py"
echo $command
eval $command