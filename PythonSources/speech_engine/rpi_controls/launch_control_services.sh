sudo killall python
sudo killall python3

RPI_PATH=$HOME/speech_engine/rpi_controls/

command="python ${RPI_PATH}/server.py &"
echo $command
eval $command

sleep 3

command="sudo python ${RPI_PATH}/controlClient.py"
echo $command
eval $command
