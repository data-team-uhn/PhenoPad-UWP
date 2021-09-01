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

/// <summary>
/// Processes JSON files received from speech engine and attemps to reconstruct
/// a conversation.
/// </summary>
namespace PhenoPad.SpeechService
{

    /// <summary>
    /// A key-value pair.
    /// </summary>
    /// <typeparam name="X">Type of the first element.</typeparam>
    /// <typeparam name="Y">Type of the second element.</typeparam>
    /// <remarks>
    /// https://stackoverflow.com/questions/166089/what-is-c-sharp-analog-of-c-stdpair
    /// </remarks>
    public class Pair<X, Y>
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

        /// <summary>
        /// Returns the length/duration of the interval.
        /// </summary>
        /// <returns>length/duration of the interval in seconds</returns>
        public double getLength()
        {
            return this.end - this.start;
        }
    }

    /// <summary>
    /// Custom object used to construct conversations. Each WordSpoken object contains
    /// an array that corresponds to the discretized time interval (with a unit of 100ms) 
    /// over which the word is uttered.
    /// * If the interval length is 0, set the array length to 1
    /// </summary>
    class WordSpoken
    {
        public string word;
        public int[] smallSegSpeakers;
        public TimeInterval interval;

        public WordSpoken(string _word, int _speaker, TimeInterval _interval)
        {
            this.word = _word;
            this.interval = _interval;
            int lengthIn100ms = Math.Max((int)(this.interval.getLength() / 0.1), 1);
            smallSegSpeakers = new int[lengthIn100ms];

            for (int i = 0; i < lengthIn100ms; i++)
            {
                this.smallSegSpeakers[i] = _speaker;
            }
        }

        /// <summary>
        /// Returns the relative start time (time since conversation started) of the 
        /// unit segment given its index.
        /// </summary>
        /// <remarks>
        /// E.g. If the word starts at 10.5s, and the index of the unit segment is 2,
        ///      the function returns 0.2s + 10.5s  = 10.7s which is the start time
        ///      of the segment.
        /// </remarks>
        public double getTimeByIndex(int index)
        {
            return index * 0.1 + this.interval.start;
        }

        /// <summary>
        /// Returns the most probable speaker of the current word.
        /// </summary>
        /// <returns>An integer (speaker index) which represents the speaker with the highest frequency,
        /// This is the speaker assigned to the word.</returns>
        /// <remarks>
        /// The most probable speaker is the one assigned to the most unit segments in the word's 
        /// interval.
        /// E.g. If a word's interval consists of 15 unit segments (each unit segment is 100ms,
        /// which means the word is 1.5s long), and 3 segments are assigned the speaker index 0,
        /// 10 are assigned the speaker index 1, 2 are assigned the index -1 (empty), then this
        /// function returns (int)1, which is the index representing "Speaker 2" (the 2nd speaker),
        /// the most likely speaker of this word.
        /// </remarks>
        public int determineSpeaker()
        {
            // Count number of segment assigned with each speaker
            Dictionary<int, int> votes = new Dictionary<int, int>();
            foreach (int i in smallSegSpeakers)
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

            // Determine the most probable speaker
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

        public Conversation conversation;               // left hand diarized bubbles    
        public Conversation realtimeConversation;       // right hand temporary grey bubbles

        private List<WordSpoken> words = new List<WordSpoken>(); // all words detected
        private List<Pair<TimeInterval, int>> diarization = new List<Pair<TimeInterval, int>>(); // a list of intervals that have speaker identified

        // Note: Each pair in diarizationSmallSegs corresponds to a 0.1s long audio segment. The audio is
        //       discretized into a set of 0.1s segments, each of which is associate with a speaker index. 
        //       If a segment contains no speech, its speaker index is -1.
        //
        //       E.g. A 2s audio segment with 2 utterances utt1 and utt2 spoken by speaker 0 and 1, respectively,
        //            can be represented as:
        //            {<0,-1>, <1,-1>, <2,-1>, <3,0>, <4,0>, <5,0>, <6,0>, <7,0>, <8,-1>, <9,-1>, <10,-1>, <11,-1>, 
        //             <12,-1>, <13,1>, <14,1>, <15,1>, <16,1>, <17,1>, <18,1>, <19,1>}
        //            where utt1 starts at 0.3s and ends at 0.8s and utt2 starts at 1.3s and ends at 2.0s.
        //       
        //       In other words, diarizationSmallSegs is the graph of the function which maps a 100ms audio segment
        //       to a speaker index.
        //
        //       The rest of the script refers to diarizationSmallSegs as the diarization graph and a 100ms audio
        //       segment an unit audio segment.
        public List<Pair<int, int>> diarizationSmallSegs = new List<Pair<int, int>>(); // Pair<(int)startTime/0.1s, speaker>

        private int diarizationWordIndex = 0;             // index of the next word to assign speakers to
        private int constructDiarizedSentenceIndex = 0;   // word index to construct diarized index => TODO: ? the next word to construct diarized sentences with ?
        private int latestSentenceIndex = 0;              // index of the next(earliest) word which has never been assigned speakers to (this is normally the
                                                          //    same as diarizationWordIndex, but does not reset when the conversation is re-diarized)

        private int lastSpeaker = 0;

        public string tempSentence;    // non-final result from engine
        public string latestSentence;  // display the last sentence to not clog up view

        public List<string> realtimeSentences = new List<string>();
        public string realtimeLastSentence;

        private int prevSentenceEnd = 0;

        /// <summary>
        /// Stores ASR results from the current notebook session.
        /// i.e. oldConversation + utterances from the current ASR session
        /// </summary>
        private List<TextMessage> currentConversation = new List<TextMessage>();

        /// <summary>
        /// Stores results from previous ASR session(s) within the current notebook session.
        /// Keeps track of utterances from previous conversations during diarization 
        /// "re-clustering" when utterances in the current ASR sessions (and consequently
        /// currentConversation's content) gets rearranged.
        /// </summary>
        private List<TextMessage> oldConversations = new List<TextMessage>();
        
        public int conversationIndex = 0;

        public int worker_pid = 0;

        public SpeechEngineInterpreter(Conversation _conv, Conversation _realtimeconv)
        {
            this.conversation = _conv;
            this.realtimeConversation = _realtimeconv;
        }

        /// <summary>
        /// Resets variables and saves last conversation when a new conversation is opened.
        /// </summary>
        /// <remarks>
        /// Called when a new recording session is started (i.e. when "Start Audio" button is clicked).
        /// </remarks>
        public void newConversation()
        {
            this.formConversation(false); // don't care about results here

            conversationIndex++;

            // Update oldConversations to the content of currentConversation till this point
            oldConversations.Clear();
            oldConversations.AddRange(this.currentConversation);

            // Clear results from last session
            words.Clear();
            realtimeSentences.Clear();
            realtimeLastSentence = String.Empty;
            tempSentence = String.Empty;
            diarization.Clear();

            // Reset diarization position markers
            this.diarizationWordIndex = 0;
            this.constructDiarizedSentenceIndex = 0;
            this.latestSentenceIndex = 0;
        }


        /// <summary>
        /// Updates speech bubbles with new ASR and diarization results.
        /// </summary>
        /// <param name="json">JSON received from the speech server, contains ASR and diarization results</param>
        public async void processJSON(SpeechEngineJSON json)
        {
            try
            {
                /** Here we take the assumption that diarization will always be slower than speech recognition. 
                 *  I.e. the diarization results of certain utterances will only be received after the final 
                 *  ASR results of these utterances.
                 *  This is because in the speech server, diarization of an utterance is only 
                 *  performed after the utterance has ended (detected using WebRTC Voice Activity Detector),
                 *  while ASR is performed real-time. Which means diarization of an utterance starts when the
                 *  ASR finishes.
                 **/

                // Update diarized speech bubbles on MainPage and SpeechPage
                if (json != null)
                {
                    if (json.worker_pid != 0)
                    {
                        this.worker_pid = json.worker_pid;
                        Debug.WriteLine("Worker PID upon processing " + this.worker_pid.ToString());
                    }

                    // If result is final, update the words list
                    // (Note: Diarization results are only sent with partial ASR results.)
                    if (json.result.final)
                    {
                        Debug.WriteLine(json.result.hypotheses[0].transcript);

                        double latest = 0; // records the ending timestamp of the last word in ASR result
                        double OFFSET = 0; // manually define offset in case word alignment data is incorrect

                        // Add new words in the result to the list of all words
                        foreach (WordAlignment wa in json.result.hypotheses[0].word_alignment)
                        {
                            double word_start = Math.Max(wa.start + json.segment_start + OFFSET, 0);
                            double word_end = Math.Max(wa.start + wa.length + json.segment_start + OFFSET, 0);
                            var w = new WordSpoken(wa.word, -1, new TimeInterval(word_start, word_end));
                            words.Add(w);
                            latest = wa.start + wa.length + json.segment_start;
                        }

                        // Update lastest timestamp to end of last sentence
                        if (latest == 0)
                        {
                            latest = words[words.Count - 1].interval.end;
                        }
                    }

                    // If received diarization result, construct TextMessages and update conversation panels
                    if (json.result.diarization != null && json.result.diarization.Count > 0)
                    {
                        bool full = false;

                        // If received new diarization of the conversation, clear previous results.
                        if (!json.result.diarization_incremental)
                        {
                            diarization.Clear();
                            diarizationSmallSegs.Clear();
                            diarizationWordIndex = 0;
                            constructDiarizedSentenceIndex = 0;
                            full = true;
                        }
                        // Update the diarization graph with new results
                        foreach (var d in json.result.diarization)
                        {
                            int speaker = d.speaker;
                            double start = d.start;
                            double end = d.end;
                            var interval = new TimeInterval(start, end);
                            this.insertToDiarizationGraph(interval, speaker);
                        }
                        // Form TextMessage objects and update conversation panels
                        this.assignSpeakerToWords();
                        this.formConversation(full);

                        // so that we don't have an overflow of words
                        this.constructTempSentence();
                    }
                }

                // Update the temp speech bubble on MainPage
                this.realtimeSentences = this.constructTempSentence();
                //
                if (!json.result.final)
                {
                    this.realtimeLastSentence = json.result.hypotheses[0].transcript.Trim();
                    
                    await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal,
                    () =>
                    {
                        // Note: tempSentence is set to "ADD NEW" after receiving a "final" result
                        if (this.tempSentence.Equals("ADD NEW"))
                        {
                            this.realtimeConversation.UpdateLastMessage(constructRealtimeTempBubble(), true);
                        }                     
                        else
                        {
                            this.realtimeConversation.UpdateLastMessage(constructRealtimeTempBubble(), false);
                        }
                        this.tempSentence = String.Empty;
                    }); 
                }
                else
                {
                    this.tempSentence = "ADD NEW";
                    this.formRealtimeConversation();
                }
            }
            catch (Exception e)
            {
                LogService.MetroLogger.getSharedLogger().Error(e.Message);
            }
        }

        /// <summary>
        /// Inserts a newly diarized interval into the diarization graph, assigns speaker arrays to words,
        /// determines the speakers of those words and groups them into messages, then adds them to the
        /// speaker panel.
        /// </summary>
        /// <remarks>
        /// This function processes diarization result messages from the speech server which have not been
        /// implemented. At the moment, diarization results are sent as a part of the ASR results, and are
        /// processed during processJSON function.
        /// </remarks>
        public async void processDiaJson(DiarizationJSON diaJson)
        {
            if (diaJson != null && diaJson.end != -1)
            {
                // *full*'s value wouldn't change right now because the DiarizationJSON class
                // doesn't contain information about "re-diarization".
                bool full = false;

                int speaker = diaJson.speaker;
                double start = diaJson.start;
                double end = diaJson.end;

                var interval = new TimeInterval(start, end);
                insertToDiarizationGraph(interval, speaker);

                assignSpeakerToWords();

                await formConversation(full);

                // so that we don't have an overflow of words (POSSIBLY OUTDATED)
                constructTempSentence();
            }
        }

        /// <summary>
        /// Returns a list of strings which contain all the words that have not been assigned speaker.
        /// </summary>
        /// <returns>a list of strings</returns>
        private List<string> constructTempSentence()
        {
            List<string> sentences = new List<string>();
            string result = String.Empty;

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

        /// <summary>
        /// (OUTDATED) Returns a TextMessage instance which represents a temporary speech bubble.
        /// </summary>
        /// <returns>TextMessage instance</returns>
        /// <remarks>
        /// This function is currently not used anywhere in the App. It is worth noting that
        /// while this function seems to construct a temp speech bubble, this.tempSentence 
        /// does not contain actual content (it's only possible assignment is "ADD NEW" - a
        /// placeholder string which indicates if the last ASR result is final). Therefore
        /// this function was likely outdated.
        /// </remarks>
        private TextMessage constructTempBubble()
        {
            var message = new TextMessage()
            {
                Body = this.tempSentence,
                Speaker = 99,
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
                IsFinal = false,
                ConversationIndex = this.conversationIndex
            };

            return message;
        }

        /// <summary>
        /// Searches for a TextMessage instance based on content and returns the instance found.
        /// </summary>
        /// <param name="body">The content of the target TextMessage instance.</param>
        /// <returns>The target TextMessage instance.</returns>
        public TextMessage GetTextMessage(string body)
        {
            var text = currentConversation.Where(x => x.Body == body).FirstOrDefault();
            return text;
        }

        #region Helper Functions for Diarization

        /// <summary>
        /// Adds the diarization result of a newly diarized utterance to the 
        /// diarization graph.
        /// </summary>
        /// <param name="interval">the interval of the utterance</param>
        /// <param name="speaker">the intteger index representing the speaker</param>
        private void insertToDiarizationGraph(TimeInterval interval, int speaker)
        {
            diarization.Add(new Pair<TimeInterval, int>(interval, speaker));

            double prev = 0;

            if (this.diarizationSmallSegs.Count > 0)
            {
                // Since each pair in the diarization graph contains the start time and speaker index of
                // an unit audio segment, the start time of the first unassigned segment is the start time
                // of the last assigned segment + 0.1s
                prev = (double)(this.diarizationSmallSegs.Last().first) / (double)(10.0) + 0.1;
            }
            if (interval.start >= prev)
            {
                // fill the space between utterances with speaker index -1
                for (int i = Convert.ToInt32(prev * 10); i < Convert.ToInt32(interval.start * 10); i += 1)
                {
                    this.diarizationSmallSegs.Add(new Pair<int, int>(i, -1));
                }

                for (int i = Convert.ToInt32(interval.start * 10); i < Convert.ToInt32(interval.end * 10); i += 1)
                {
                    this.diarizationSmallSegs.Add(new Pair<int, int>(i, speaker));
                }
            }
            else
            {
                // If the new utterance starts before the last diarized segment ends (i.e. overlapping diarization results), 
                // simply overwrite the overlapping segments.
                for (int i = Convert.ToInt32(prev * 10); i < Convert.ToInt32(interval.end * 10); i += 1)
                {
                    this.diarizationSmallSegs.Add(new Pair<int, int>(i, speaker));
                }
            }
        }

        /// <summary>
        /// Assign speakers from the diarized unit segments to each corresponding word's speaker array.
        /// </summary>
        /// <remarks>
        /// Each word (WordSpoken object) contains an array *smallSegSpeakers* whose elements are segments 
        /// which correspond to unit segments in the diarization graph. This function assigns the speaker of
        /// each segment in the diarization graph to the corresponding segment in the words' *smallSegSpeakers* array.
        /// </remarks>
        private void assignSpeakerToWords()
        {
            this.constructDiarizedSentenceIndex = this.diarizationWordIndex; // construct diarized sentences from previous diarized word index

            // Assign speakers to each word's unit segment array
            bool brokeOut = false;
            for (int wordIndex = this.diarizationWordIndex; wordIndex < this.words.Count; wordIndex++)
            {
                WordSpoken word = this.words[wordIndex];
                for (int i = 0; i < word.smallSegSpeakers.Length; i++)
                {
                    double segStartTime = word.getTimeByIndex(i);
                    int diarizationSmallSegIndex = (int)(segStartTime / 0.1);
                    
                    // Note: If diarization results hasn't caught up with ASR results,
                    //       the index of the next word to assign speaker to is the index
                    //       of the first word whose start time in 100ms is larger than the
                    //       length of the diarization graph.
                    if (diarizationSmallSegIndex >= this.diarizationSmallSegs.Count)
                    {
                        this.diarizationWordIndex = wordIndex;
                        brokeOut = true;
                        break;
                    }
                    else
                    {
                        word.smallSegSpeakers[i] = this.diarizationSmallSegs[diarizationSmallSegIndex].second;
                    }
                }
            }

            if (!brokeOut)
            {
                this.diarizationWordIndex = this.words.Count;
            }

            this.latestSentenceIndex = this.diarizationWordIndex;
        }

        public async Task<List<Phenotype>> queryPhenoService(string text)
        {
            var phenos = await PhenotypeManager.getSharedPhenotypeManager().annotateByNCRAsync(text);
            if (phenos == null)
                return new List<Phenotype>();

            //only log from speech if there are phenotypes detected,otherwise would be really crowded
            if (phenos.Keys.Count() > 0)
                OperationLogger.getOpLogger().Log(OperationType.Speech, text, phenos);

            List<Phenotype> phenolist = new List<Phenotype>();
            foreach (var key in phenos.Keys)
            {
                phenolist.Add(phenos[key]);
            }

            if (phenolist != null && phenolist.Count > 0)
            {
                // need this runasync function otherwise will give threading error
                Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal,
                () =>
                {
                    foreach (var p in phenolist)
                    {
                        PhenotypeManager.getSharedPhenotypeManager().addPhenotypeCandidate(p, SourceType.Speech);
                        p.sourceType = SourceType.Speech;
                    }
                });

            }
            return phenolist;
        }

        // NOTE: this function doesn't need to return list since its return value is never used.
        /// <summary>
        /// Form TextMessage objects with words and update ongoing and past conversation panels
        /// with these TextMessages.
        /// </summary>
        /// <param name="full">whether the conversation has been re-diarized</param>
        /// <returns>The newly formed TextMessage objects.</returns>
        private async Task<List<TextMessage>> formConversation(bool full)
        {
            List<TextMessage> messages = new List<TextMessage>();

            double prevStart = 0;
            double speechEnd = 0;
            int prevSpeaker = this.lastSpeaker;
            string sentence = String.Empty;

            // Form sentences with newly diarized words and form TextMessage objects with sentences
            // Note: If all sentences re-diarized, *constructDiarizedSentenceIndex* is reset to 0 and this
            //       loop reconstructs sentences & TMs with all words.

            
            //for (int wordIndex = this.constructDiarizedSentenceIndex; wordIndex < this.diarizationWordIndex; wordIndex++)
            for (int wordIndex = prevSentenceEnd; wordIndex < words.Count; wordIndex++)
            {
                prevSentenceEnd = words.Count;

                if (prevStart == 0)
                    prevStart = words[wordIndex].interval.start;

                // Determine word speaker
                int wordProposedSpeaker = this.words[wordIndex].determineSpeaker();
                if (wordProposedSpeaker == -1)
                    wordProposedSpeaker = prevSpeaker;

                // If detected speaker change or "." encountered, save sentence as TextMessage
                if  ( words[wordIndex].word.Contains(".")||words[wordIndex].word.Contains("?") ) //&& sentence.Length > 50, (wordProposedSpeaker != prevSpeaker && sentence.Length != 0) ||
                {
                    Debug.WriteLine("speechengineinterpreter in for loop creating message");
                    sentence += " " + words[wordIndex].word;
                    //Debug.WriteLine(wordIndex);
                    //Debug.WriteLine(constructDiarizedSentenceIndex);
                    Debug.WriteLine(prevSentenceEnd);
                    Debug.WriteLine(words.Count);
                    Debug.WriteLine(words[wordIndex].word);
                    Debug.WriteLine(sentence);

                    var phenotypes = await queryPhenoService(sentence);
                    bool hasPheno = phenotypes.Count > 0 ? true : false;
                    var message = new TextMessage()
                    {
                        Body = sentence,
                        Speaker = (uint)prevSpeaker,
                        DisplayTime = DateTime.Now,
                        Interval = new TimeInterval(prevStart, words[wordIndex - 1].interval.end),
                        IsFinal = true,
                        ConversationIndex = this.conversationIndex,
                        AudioFile = SpeechManager.getSharedSpeechManager().GetAudioName(),
                        //phenotypesInText = new List<Phenotype>()
                        phenotypesInText = phenotypes,
                        hasPhenotype = hasPheno
                    };
                    messages.Add(message);
                    prevSpeaker = wordProposedSpeaker;
                    sentence = String.Empty;
                    //sentence += " " + words[wordIndex].word;
                    prevStart = words[wordIndex].interval.start;
                }
                // Otherwise, add word to current sentence
                else
                {
                    sentence += " " + words[wordIndex].word;
                    speechEnd = words[wordIndex].interval.end;
                }
            }

            //if (sentence.Length > 0 && prevSpeaker != -1)
            //{
            //    var phenotypes = await queryPhenoService(sentence);
            //    bool hasPheno =  phenotypes.Count > 0 ? true : false;
            //    var m = new TextMessage()
            //    {
            //        Body = sentence,
            //        Speaker = (uint)prevSpeaker,
            //        DisplayTime = DateTime.Now,
            //        Interval = new TimeInterval(prevStart, speechEnd),
            //        IsFinal = true,
            //        ConversationIndex = this.conversationIndex,
            //        AudioFile = SpeechManager.getSharedSpeechManager().GetAudioName(),
            //        phenotypesInText = phenotypes,
            //        hasPhenotype = hasPheno
            //    };
            //    messages.Add(m);
            //}

            this.lastSpeaker = prevSpeaker;

            // Update conversation panels with new TextMessages
            await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal,
            () =>
            {
                // All sentences re-diarized, update ongoing conversation panel
                // Note: *full* denotes if the conversation has beeb re-diarized 
                //       (i.e. if speakers of all utterances in the conversation
                //       have been re-calculated).
                if (full)
                {
                    // Note: *messages* are the list of rearranged TextMessage objects of the entire ASR session,
                    //       *conversation* (realtime speech panel) and *currentConversation* are updated to TMs
                    //       from the past sessions (in the current notebook session) + the rearranged TMs from this ASR session.  
                    List<TextMessage> temp = new List<TextMessage>(oldConversations);
                    temp.AddRange(messages);
                    this.conversation.ClearThenAddRange(temp);
                    currentConversation = temp;

                    MainPage.Current.conversations.AddRange(temp);
                    SpeechPage.Current.updateChat();
                    TestPage.Current.updateChat();
                }
                // Received new diarized TMs, update ongoing and past conversation panels
                else
                {
                    List<TextMessage> temp = new List<TextMessage>();
                    temp.AddRange(messages);
                    this.conversation.AddRange(temp);
                    currentConversation.AddRange(temp);
                    MainPage.Current.conversations.AddRange(temp);
                    
                    SpeechPage.Current.updateChat();
                    TestPage.Current.updateChat();
                }
            });

            return messages;
        }

        /// <summary>
        /// Replaces the TextMessage instances in the real-time conversation collection
        /// with the list of ones that have not been assigned speakers.
        /// </summary>
        /// <remarks>
        /// The "real-time" conversation collection (this.realtimeConversation) are the items
        /// displayed as the temp grey speech bubbles.
        /// </remarks>
        private async void formRealtimeConversation()
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

            await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal,
            () =>
            {
                this.realtimeConversation.ClearThenAddRange(messages);
            });
        }

        /// <summary>
        /// Displays the start time, end time and speaker of all diarized utterances in the ongoing conversation to the output window.
        /// </summary>
        public void printDiarizationResult()
        {
            for (int i = 0; i < diarization.Count; i++)
            {
                Debug.WriteLine("Start: " + diarization[i].first.start.ToString() + " End: " + diarization[i].first.end.ToString() + " => Speaker " + diarization[i].second.ToString());
            }

            Debug.WriteLine("---------------------");
        }

        /// <summary>
        /// Finds the first JSON block and returns its content as string.
        /// </summary>
        /// <param name="content">the content to process (parse the first json block from)</param>
        /// <param name="outContent">stores content after the first json block for future use</param>
        /// <returns>if JSON block found: the content of the first json block as string; else: empty string</returns>
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
                string toReturn = content.Substring(0, index + 1);
                if (content.Length >= index + 1)
                {
                    outContent = content.Substring(index + 1); // note that content is a reference
                }
                return toReturn;
            }
            else
            {
                return string.Empty;
            }
        }

        /// <summary>
        /// Finds all instances of a phenotype in the list of current TextMessage instances and updates their state.
        /// </summary>
        /// <param name="pheno">The phenotype whose state is updated.</param>
        public void UpdatePhenotypeState(Phenotype pheno)
        {
            var txtMsg = this.currentConversation.Where(x => x.phenotypesInText.Contains(pheno)).ToList();
            if (txtMsg.Count > 0)
            {
                foreach (var msg in txtMsg)
                {
                    int phenoIdx = msg.phenotypesInText.IndexOf(pheno);
                    msg.phenotypesInText[phenoIdx].state = pheno.state;
                }
                Debug.WriteLine("updated phenotype states in speechinterpreter.");
            }
        }

        /// <summary>
        /// Removes all instances of a phenotype from the list of current TextMessage instances.
        /// </summary>
        /// <param name="pheno">The phenotype to be deleted</param>
        public void DeletePhenotype(Phenotype pheno)
        {
            var txtMsg = this.currentConversation.Where(x => x.phenotypesInText.Contains(pheno)).ToList();
            if (txtMsg.Count > 0)
            {
                foreach (var msg in txtMsg)
                {
                    int phenoIdx = msg.phenotypesInText.IndexOf(pheno);
                    msg.phenotypesInText.RemoveAt(phenoIdx);
                }
                Debug.WriteLine("deleted phenotype states in speechinterpreter.");
            }
        }

        /// <summary>
        /// Checks if the on-going speech session has any content.
        /// </summary>
        /// <returns>true if the list of current TextMessage instances is not empty; false otherwise.</returns>
        public bool CurrentConversationHasContent()
        {
            return currentConversation.Count > 0;
        }

        #endregion
    }
}
