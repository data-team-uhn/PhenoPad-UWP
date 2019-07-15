using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using Windows.UI.Core;
using PhenoPad.PhenotypeService;
using PhenoPad.FileService;
using PhenoPad.LogService;

namespace PhenoPad.SpeechService
{
    // Processes JSON files received from speech engine and attemps to reconstruct
    // a conversation

    public class Pair<X, Y>
    // https://stackoverflow.com/questions/166089/what-is-c-sharp-analog-of-c-stdpair
    {
        private X _x;
        private Y _y;

        public Pair(X first, Y second)
        {
            _x = first;
            _y = second;
        }

        public X first { get { return _x; } }

        public Y second { get { return _y; } }
    }

    public class TimeInterval
    {
        public double start { get; set; }
        public double end { get; set; }

        public TimeInterval(double _start, double _end)
        {
            this.start = _start;
            this.end = _end;
        }

        public double getLength()
        {
            return this.end - this.start;
        }
    }

    // We construct the entire conversation via words spoken
    // contains an array that corresponds to each 0.1 seconds in time interval
    // lenght 1 if interval is 0 lengthed
    class WordSpoken
    {
        public string word;
        public int[] smallSegSpeaker;
        public TimeInterval interval;

        public WordSpoken(string _word, int _speaker, TimeInterval _interval)
        {
            this.word = _word;
            this.interval = _interval;
            int speakerLength = Math.Max((int)(this.interval.getLength() / 0.1), 1);
            smallSegSpeaker = new int[speakerLength];

            for (int i = 0; i < speakerLength; i++)
            {
                this.smallSegSpeaker[i] = _speaker;
            }
        }

        public double getTimeByIndex(int index)
        {
            return index * 0.1 + this.interval.start;
        }

        public int determineSpeaker()
        {
            Dictionary<int, int> votes = new Dictionary<int, int>();
            foreach (int i in smallSegSpeaker)
            {
                if (votes.ContainsKey(i))
                {
                    votes[i]++;
                }
                else
                {
                    votes[i] = 1;
                }
            }

            int highestVotes = 0;
            int highestSpeaker = -1;
            foreach (KeyValuePair<int, int> entry in votes)
            {
                if (entry.Value > highestVotes)
                {
                    highestSpeaker = entry.Key;
                    highestVotes = entry.Value;
                }
            }
            return highestSpeaker;
        }
    }

    public class SpeechEngineInterpreter
    {
        // to be connected to speech manager
        public Conversation conversation;               // left hand diarized pannel    
        public Conversation realtimeConversation;       // right hand temporary result panel

        private List<WordSpoken> words = new List<WordSpoken>();      // all words detected
        // a list of intervals that have speaker identified
        private List<Pair<TimeInterval, int>> diarization = new List<Pair<TimeInterval, int>>();    
        public List<Pair<int, int>> diarizationSmallSeg = new List<Pair<int, int>>();        // Pair<start time, speaker>
        private int diarizationWordIndex = 0;                     // word index that need to be assigned speaker
        private int constructDiarizedSentenceIndex = 0;           // word index to construct diarized index
        private int latestSentenceIndex = 0;                      // word index that has not been diarized
        private int lastSpeaker = 0;

        public string tempSentence;         // non-final result from engine
        public string latestSentence;       // display the last sentence to not clog up view

        public List<string> realtimeSentences = new List<string>();
        public string realtimeLastSentence;

        private List<TextMessage> currentConversation = new List<TextMessage>();
        private List<TextMessage> oldConversations = new List<TextMessage>();
        public int conversationIndex = 0;

        public int worker_pid = 0;

        // Empty constructor :D
        public SpeechEngineInterpreter(Conversation _conv, Conversation _realtimeconv)
        {
            this.conversation = _conv;
            this.realtimeConversation = _realtimeconv;
        }

        public void newConversation()
        {
            this.formConversation(false);       // don't care about results here
            oldConversations.AddRange(this.currentConversation);

            words.Clear();
            realtimeSentences.Clear();
            realtimeLastSentence = String.Empty;
            tempSentence = String.Empty;
            diarization.Clear();

            conversationIndex++;

            this.diarizationWordIndex = 0;
            this.constructDiarizedSentenceIndex = 0;
            this.latestSentenceIndex = 0;
        }

        public void queryPhenoService(string text)
        {
            Task<Dictionary<string, Phenotype>> phenosTask = PhenotypeManager.getSharedPhenotypeManager().annotateByNCRAsync(text);

            phenosTask.ContinueWith(_ =>
            {

                if (phenosTask.Result == null)
                    return;
                //only log from speech if there are phenotypes detected,otherwise would be really crowded
                if (phenosTask.Result.Keys.Count() > 0)
                    OperationLogger.getOpLogger().Log(OperationType.Speech, text, phenosTask.Result);

                List<Phenotype> list = new List<Phenotype>();
                foreach(var key in phenosTask.Result.Keys)
                {
                    list.Add(phenosTask.Result[key]);
                }

                if (list != null && list.Count > 0)
                {
                    //Debug.WriteLine("We detected at least " + list[0].name);

                    list.Reverse();
                    //need this runasync function otherwise will give threading error
                    Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal,
                    () =>
                    {
                        foreach (var p in list)
                        {
                            PhenotypeManager.getSharedPhenotypeManager().addPhenotypeCandidate(p, SourceType.Speech);
                        }
                    }
                    );

                }
            });
        }

        // Looks at speech engine result to identify what can be done
        public async void processJSON(SpeechEngineJSON json)
        {
            //processes speech result received from server
            try
            {
                // We take for granted that diarizatio will always be a lot slower than 
                // speech recognition

                if (json != null)
                {
                    if (json.worker_pid != 0)
                    {
                        this.worker_pid = json.worker_pid;
                        Debug.WriteLine("Worker PID upon processing " + this.worker_pid.ToString());
                    }

                    // First check if speech is final (remember that diarization is always slower)
                    if (json.result.final)
                    {
                        this.queryPhenoService(json.result.hypotheses[0].transcript);

                        //Debug.WriteLine(json.result.hypotheses[0].transcript);

                        // Then we should have a bunch of words to look at
                        double latest = 0;
                        double OFFSET = 0;          // not too sure if word alignment data is actually correct from aspire???
                        foreach (WordAlignment wa in json.result.hypotheses[0].word_alignment)
                        {
                            // Remove <laughter> <unk> etc.
                            //if (wa.word.IndexOf('[') == -1 && wa.word.IndexOf('<') == -1)
                            {
                                // Make sure word does not start before 0
                                double word_start = Math.Max(wa.start + json.segment_start + OFFSET, 0);
                                double word_end = Math.Max(wa.start + wa.length + json.segment_start + OFFSET, 0);
                                var w = new WordSpoken(wa.word, -1, new TimeInterval(word_start, word_end));
                                words.Add(w);
                                latest = wa.start + wa.length + json.segment_start;
                            }
                        }

                        if (latest == 0)
                        {
                            latest = words[words.Count - 1].interval.end;
                        }
                        //Debug.WriteLine("Latest final sentence up to " + latest.ToString());

                        /*if (json.result.hypotheses[0].word_alignment.Count > 0)
                        {
                            words.Add(new WordSpoken(".", -1, new TimeInterval(latest, latest)));
                        }*/
                    }

                    // Then check if we have results from diarization
                    if (json.result.diarization != null && json.result.diarization.Count > 0)
                    {
                        bool full = false;
                        if (!json.result.diarization_incremental)
                        {
                            //Debug.WriteLine("Received new diarization. Removing previous results.");
                            this.diarization.Clear();
                            this.diarizationSmallSeg.Clear();
                            this.diarizationWordIndex = 0;
                            this.constructDiarizedSentenceIndex = 0;
                            full = true;
                        }
                        foreach (var d in json.result.diarization)
                        {
                            int speaker = d.speaker;
                            //Debug.WriteLine("Identified speaker is " + speaker.ToString());
                            double start = d.start;
                            double end = d.end;

                            var interval = new TimeInterval(start, end);
                            this.insertToDiarization(interval, speaker);
                        }

                        this.assignSpeakerToWords();
                        //Debug.WriteLine("Diarized to word count" + diarizationWordIndex.ToString());
                        //Debug.WriteLine("Total word count" + words.Count.ToString());
                        //Debug.WriteLine(json.original);
                        this.formConversation(full);

                        // so that we don't have an overflow of words
                        this.constructTempSentence();
                        //this.printDiarizationResult();
                    }

                }

                this.realtimeSentences = this.constructTempSentence();

                if (!json.result.final)
                {
                    this.realtimeLastSentence = json.result.hypotheses[0].transcript.Trim();

                    await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal,
                    () =>
                    {
                        if (this.tempSentence.Equals("ADD NEW"))
                        {
                            this.realtimeConversation.UpdateLastMessage(this.constructRealtimeTempBubble(), true);
                            //Debug.WriteLine("Not final, no existing");
                        }
                        else
                        {
                            this.realtimeConversation.UpdateLastMessage(this.constructRealtimeTempBubble(), false);
                            //Debug.WriteLine("Not final, but has existing");
                        }
                        this.tempSentence = String.Empty;
                    }
                    );
                }
                else
                {
                    this.tempSentence = "ADD NEW";
                    this.formRealtimeConversation();
                    //Debug.WriteLine("Final");
                }

                // latest sentence has a length cap
                //this.constructLatestSentence();
            }
            catch (Exception e) {
                LogService.MetroLogger.getSharedLogger().Error(e.Message);
            }


        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="diaJson"></param>
        public void processDiaJson(DiarizationJSON diaJson)
        {
            // Then check if we have results from diarization
            //if (json.result.diarization != null && json.result.diarization.Count > 0)
            if (diaJson != null && diaJson.end != -1)
            {
                bool full = false;
                //foreach (var d in json.result.diarization)
                //{
                int speaker = diaJson.speaker;
                //Debug.WriteLine("Identified speaker is " + speaker.ToString());
                double start = diaJson.start;
                double end = diaJson.end;

                var interval = new TimeInterval(start, end);
                this.insertToDiarization(interval, speaker);
                //}

                this.assignSpeakerToWords();
                //Debug.WriteLine("Diarized to word count" + diarizationWordIndex.ToString());
                //Debug.WriteLine("Total word count" + words.Count.ToString());
                //Debug.WriteLine(json.original);
                this.formConversation(full);

                // so that we don't have an overflow of words
                this.constructTempSentence();
                //this.printDiarizationResult();
            }
        }

    // Show all words that have not been diarized
        private List<string> constructTempSentence()
            {
                List<string> sentences = new List<string>();
                string result = String.Empty;

                /*Debug.WriteLine("constructing latest sentences from word index " +
                            this.latestSentenceIndex.ToString() + " to " +
                            this.words.Count.ToString());*/
                for (int i = this.latestSentenceIndex; i < this.words.Count; i++)
                {
                    result += " " + words[i].word;

                    if (result.Length > 120)
                    {
                        sentences.Add(result);
                        result = String.Empty;
                    }
                }

                if (result.Length > 0)
                {
                    sentences.Add(result);
                }

                return sentences;
            }

        private TextMessage constructTempBubble()
        {
            var message = new TextMessage()
            {
                Body = this.tempSentence,
                Speaker = 99,
                //DisplayTime = DateTime.Now,
                IsFinal = false,
                ConversationIndex = this.conversationIndex
            };

            return message;
        }

        private TextMessage constructRealtimeTempBubble()
        {
            var message = new TextMessage()
            {
                Body = this.realtimeLastSentence,
                Speaker = 99,
                //DisplayTime = DateTime.Now,
                //Interval = new TimeInterval(start, start + length),
                IsFinal = false,
                ConversationIndex = this.conversationIndex
            };

            return message;
        }

        public TextMessage GetTextMessage(string body) {
            var tx = currentConversation.Where(x => x.Body == body).FirstOrDefault();
            return tx;
        }


        // ------ Helper Functions for Diarization ---------------

        // Fill in diarizaitonSmallSeg by reading interval
        private void insertToDiarization(TimeInterval interval, int speaker)
        {
            this.diarization.Add(new Pair<TimeInterval, int>(interval, speaker));

            double prev = 0;
            if (this.diarizationSmallSeg.Count > 0)
            {
                prev = (double)(this.diarizationSmallSeg.Last().first) / (double)(10.0) + 0.1;
            }
            if (interval.start >= prev)
            {
                // fill empty ones, just in case
                for (int i = Convert.ToInt32(prev * 10); i < Convert.ToInt32(interval.start * 10); i += 1)
                {
                    this.diarizationSmallSeg.Add(new Pair<int, int>(i, -1));
                    //Debug.WriteLine("addedSmallSegIndex: " + (diarizationSmallSeg.Count - 1));
                    //Debug.WriteLine("diarizationSmallSeg added: " + (i, -1));
                }

                for (int i = Convert.ToInt32(interval.start * 10); i < Convert.ToInt32(interval.end * 10); i += 1)
                {
                    this.diarizationSmallSeg.Add(new Pair<int, int>(i, speaker));
                    //Debug.WriteLine("addedSmallSegIndex: " + (diarizationSmallSeg.Count - 1));
                    //Debug.WriteLine("diarizationSmallSeg added: " + (i, speaker));
                }
            }
            else
            {
                for (int i = Convert.ToInt32(prev * 10); i < Convert.ToInt32(interval.end * 10); i += 1)
                {
                    this.diarizationSmallSeg.Add(new Pair<int, int>(i, speaker));
                    //Debug.WriteLine("addedSmallSegIndex: " + (diarizationSmallSeg.Count - 1));
                    //Debug.WriteLine("diarizationSmallSeg added: " + (i, speaker));
                }
            }
        }

        // Label speaker for each word according to speaker intervals
        // returns index to word that has been diarized
        private void assignSpeakerToWords()
        {
            // construct diarized sentences from previous diarized word index
            this.constructDiarizedSentenceIndex = this.diarizationWordIndex;
            /*Debug.WriteLine("assigning words from word index " +
                        this.diarizationWordIndex.ToString() + " to " +
                        this.words.Count.ToString());*/

            bool brokeOut = false;
            for (int wordIndex = this.diarizationWordIndex; wordIndex < this.words.Count; wordIndex++)
            {
                WordSpoken word = this.words[wordIndex];
                for (int i = 0; i < word.smallSegSpeaker.Length; i++)
                {
                    double time = word.getTimeByIndex(i);
                    int diarizationSmallSegIndex = (int)(time / 0.1);

                    if (diarizationSmallSegIndex >= this.diarizationSmallSeg.Count)
                    {
                        this.diarizationWordIndex = wordIndex;
                        brokeOut = true;
                        break;
                    }
                    else
                    {
                        word.smallSegSpeaker[i] = this.diarizationSmallSeg[diarizationSmallSegIndex].second;
                    }
                }
            }

            // latest sentence should start from last diarized word index, but not 
            // affected by re-training
            if (!brokeOut)
            {
                this.diarizationWordIndex = this.words.Count;
            }

            this.latestSentenceIndex = this.diarizationWordIndex;
        }

        private async void AnnotateTextMessage(TextMessage tm) {
            
            var result = await PhenotypeManager.getSharedPhenotypeManager().annotateByNCRAsync(tm.Body);
            if (result.Count > 0) {
                foreach (var pheno in result.Values.ToList()) {
                    pheno.sourceType = SourceType.Speech;
                }
                tm.phenotypesInText.AddRange(result.Values.ToList());
                tm.hasPhenotype = true;
                Debug.WriteLine($"added {result.Values.Count} phenotypes to list");
            }
        }


        // Concatenate words together to form sentences
        // awkward thing is we don't know how to get sentences
        private async Task<List<TextMessage>> formConversation(bool full)
        {
            List<TextMessage> messages = new List<TextMessage>();

            double prevStart = 0;
            double speechEnd = 0;
            int prevSpeaker = this.lastSpeaker;
            string sentence = String.Empty;
            /*
            Debug.WriteLine("Forming conversation from word index " +
                        this.constructDiarizedSentenceIndex.ToString() + " to " +
                        this.diarizationWordIndex.ToString());*/

            for (int wordIndex = this.constructDiarizedSentenceIndex; wordIndex < this.diarizationWordIndex; wordIndex++)
            {   
                // then won't trigger again
                if (prevStart == 0)
                    prevStart = words[wordIndex].interval.start;

                //tries to determine the speaker, if unknown by default assigns to last speaker
                int wordProposedSpeaker = this.words[wordIndex].determineSpeaker();
                if (wordProposedSpeaker == -1)
                    wordProposedSpeaker = prevSpeaker;

                //pushes messages if the speaker if different or exceeded char limit 50
                if ((wordProposedSpeaker != prevSpeaker && sentence.Length != 0) || (words[wordIndex].word == "." && sentence.Length > 50))
                {
                    Debug.WriteLine("speechengineinterpreter in for loop creating message");
                    var message = new TextMessage()
                    {
                        Body = sentence + ".",
                        Speaker = (uint)prevSpeaker,
                        DisplayTime = DateTime.Now,
                        Interval = new TimeInterval(prevStart, words[wordIndex - 1].interval.end),
                        IsFinal = true,
                        ConversationIndex = this.conversationIndex,
                        AudioFile = SpeechManager.getSharedSpeechManager().GetAudioName(),
                        phenotypesInText = new List<Phenotype>()
                                             
                    };
                    messages.Add(message);
                    prevSpeaker = wordProposedSpeaker;
                    sentence = String.Empty;
                    sentence += " " + words[wordIndex].word;
                    prevStart = words[wordIndex].interval.start;
                }
                //if same speaker, add words to current sentence
                else
                {
                    sentence += " " + words[wordIndex].word;
                    speechEnd = words[wordIndex].interval.end;
                }
            }

            // Do not add new sentence if it is empty
            if (sentence.Length > 0 && prevSpeaker != -1)
            {

                var m = new TextMessage()
                {
                    Body = sentence,
                    Speaker = (uint)prevSpeaker,
                    Interval = new TimeInterval(prevStart, speechEnd),
                    IsFinal = true,
                    DisplayTime = DateTime.Now,
                    ConversationIndex = this.conversationIndex,
                    AudioFile = SpeechManager.getSharedSpeechManager().GetAudioName(),
                    phenotypesInText = new List<Phenotype>()
                };
                var result = await PhenotypeManager.getSharedPhenotypeManager().annotateByNCRAsync(sentence);
                if (result.Count > 0)
                {
                    foreach (var pheno in result.Values.ToList())
                    {
                        pheno.sourceType = SourceType.Speech;
                    }
                    m.phenotypesInText.AddRange(result.Values.ToList());
                    m.hasPhenotype = true;
                    Debug.WriteLine($"added {result.Values.Count} phenotypes to list");
                }
                messages.Add(m);
            }

            this.lastSpeaker = prevSpeaker;

            /*Debug.WriteLine("Forming a conversation of length " + messages.Count.ToString());*/

            await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal,
            () =>
            {
                if (full)
                {
                    List<TextMessage> temp = new List<TextMessage>(oldConversations);
                    temp.AddRange(messages);
                    this.conversation.ClearThenAddRange(temp);
                    currentConversation = temp;
                }
                else
                {
                    List<TextMessage> temp = new List<TextMessage>();
                    temp.AddRange(messages);
                    this.conversation.AddRange(temp);
                    currentConversation.AddRange(temp);
                    //MainPage.Current.conversations.AddRange(temp);
                    //SpeechPage.Current.updateChat();

                }

            }
            );

            return messages;
        }

        //adding temporary text messages
        private void formRealtimeConversation()
        {
            List<TextMessage> messages = new List<TextMessage>();

            foreach (string sentence in this.realtimeSentences)
            {
                if (sentence.Length > 0)
                {
                    var m = new TextMessage()
                    {
                        Body = sentence,
                        Speaker = (uint)99,
                        IsFinal = false,
                        ConversationIndex = this.conversationIndex
                    };
                    messages.Add(m);
                }
            }


            Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal,
            () =>
            {
                //this.conversation.Clear();
                this.realtimeConversation.ClearThenAddRange(messages);
            }
            );
        }

        public void printDiarizationResult()
        {
            for (int i = 0; i < diarization.Count; i++)
            {
                Debug.WriteLine("Start: " + diarization[i].first.start.ToString() + " End: " + diarization[i].first.end.ToString() + " => Speaker " + diarization[i].second.ToString());
            }

            /*for (int i = 0; i < words.Count; i++)
            {
                Debug.WriteLine("[" + i.ToString() + "] " + words[i].word + " Start: " + words[i].interval.start + " => Speaker " + words[i].speaker.ToString());
            }*/

            Debug.WriteLine("---------------------");
        }

        public static string getFirstJSON(string content, out string outContent)
        {
            int count = 0;
            int index = 0;
            bool good = false;
            outContent = content;
            foreach (char c in content)
            {
                if (c == '{')
                {
                    count += 1;
                }
                else if (c == '}')
                {
                    count -= 1;
                    if (count == 0)
                    {
                        good = true;
                        break;
                    }
                }
                index += 1;
            }

            if (good)
            {
                //Debug.WriteLine((content.Length).ToString() + ", " + (index).ToString());
                string toReturn = content.Substring(0, index + 1);
                if (content.Length >= index + 1)
                {
                    outContent = content.Substring(index + 1);     // note that content is a reference
                }
                return toReturn;
            }
            else
            {
                return string.Empty;
            }
        }

        public void UpdatePhenotypeState(Phenotype pheno) {
            var tm = this.currentConversation.Where(x => x.phenotypesInText.Contains(pheno)).ToList();
            if (tm.Count > 0) {
                foreach (var mess in tm) {
                   int ind = mess.phenotypesInText.IndexOf(pheno);
                    mess.phenotypesInText[ind].state = pheno.state;


                }
                Debug.WriteLine("updated phenotype states in speechinterpreter.");
            }
        }

        // save transcriptions
        public async Task SaveCurrentConversationsToDisk()
        {
            int finalCount = 0;
            foreach (TextMessage msg in currentConversation) {
                if (msg.IsFinal)
                    finalCount++;
            }
            Debug.WriteLine($"saving transcripts: number of final messages = {finalCount}");
            if (currentConversation.Count > 0 && finalCount > 0)
            {//only save transcripts if there are finalized messages

                try
                {
                    MetroLogger.getSharedLogger().Info($"Saving final messages of current conversation to transcript_{this.conversationIndex}");
                    string fpath = FileManager.getSharedFileManager().GetNoteFilePath(
                      FileManager.getSharedFileManager().currentNoteboookId,
                      "",
                      NoteFileType.Transcriptions,
                      "transcriptions_" + conversationIndex);
                    Debug.WriteLine("transcript will be saved to " + fpath);
                    var result = await FileManager.getSharedFileManager().SaveObjectSerilization(fpath, currentConversation, typeof(List<TextMessage>));
                    Debug.WriteLine("transcript save result = " + result);
                }
                catch (Exception e)
                {
                    MetroLogger.getSharedLogger().Error("Failed to save current conversations transcriptions into disk: " + e.Message);
                }
            }
           }

        public void addToCurrentConversation(TextMessage t) {
            currentConversation.Add(t);
        }

    }
}
