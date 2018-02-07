﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using Windows.UI.Core;
using PhenoPad.PhenotypeService;

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
    }

    // We construct the entire conversation via words spoken
    class WordSpoken
    {
        public string word;
        public int speaker;
        public TimeInterval interval;

        public WordSpoken(string _word, int _speaker, TimeInterval _interval)
        {
            this.word = _word;
            this.speaker = _speaker;
            this.interval = _interval;
        }
    }

    public class SpeechEngineInterpreter
    {
        private List<WordSpoken> words = new List<WordSpoken>();      // all words detected
        public string tempSentence;         // non-final result from engine
        public List<string> realtimeSentences = new List<string>();
        public string realtimeLastSentence;
        public string latestSentence;       // display the last sentence to not clog up view
        private int diarizationWordIndex = 0;
        private List<Pair<TimeInterval, int>> diarization = new List<Pair<TimeInterval, int>>();  // a list of intervals that have speaker identified

        public Conversation conversation;       // to be connected to speech manager
        public Conversation realtimeConversation;

        private List<TextMessage> oldConversations = new List<TextMessage>();

        public int conversationIndex = 0;

        // Empty constructor :D
        public SpeechEngineInterpreter(Conversation _conv, Conversation _realtimeconv)
        {
            this.conversation = _conv;
            this.realtimeConversation = _realtimeconv;
        }

        public void newConversation()
        {
            var convo = this.formConversation();

            oldConversations.AddRange(convo);
            words.Clear();
            realtimeSentences.Clear();
            realtimeLastSentence = String.Empty;
            tempSentence = String.Empty;
            diarization.Clear();
            diarizationWordIndex = 0;

            conversationIndex++;
        }

        private void queryPhenoService(string text)
        {
            Task<List<Phenotype>> phenosTask = PhenotypeManager.getSharedPhenotypeManager().annotateByNCRAsync(text);
                
            phenosTask.ContinueWith(_ =>
            {
                List<Phenotype> list = phenosTask.Result;
                    
                if (list != null && list.Count > 0)
                {
                    Debug.WriteLine("We detected at least " + list[0].name);

                    list.Reverse();
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
        public void processJSON(SpeechEngineJSON json)
        {
            // We take for granted that diarizatio will always be a lot slower than 
            // speech recognition
            
            // First check if speech is final (remember that diarization is always slower)
            if (json.result.final)
            {
                this.queryPhenoService(json.result.hypotheses[0].transcript);

                // Then we should have a bunch of words to look at
                double latest = 0;
                foreach (WordAlignment wa in json.result.hypotheses[0].word_alignment)
                {
                    // Remove <laughter> <unk> etc.
                    if (wa.word.IndexOf('[') == -1 && wa.word.IndexOf('<') == -1)
                    {
                        var w = new WordSpoken(wa.word, -1, new TimeInterval(wa.start + json.segment_start, wa.start + wa.length + json.segment_start));
                        words.Add(w);
                        latest = wa.start + wa.length + json.segment_start;
                    }
                }

                if (latest == 0)
                {
                    latest = words[words.Count - 1].interval.end;
                }

                if (json.result.hypotheses[0].word_alignment.Count > 0)
                {
                    words.Add(new WordSpoken(".", -1, new TimeInterval(latest, latest)));
                }

                // because transcrip is final and has already been accounted for
                this.tempSentence = this.constructTempSentence();
                //Debug.WriteLine(json.result.hypotheses[0].transcript.Trim());

                this.realtimeSentences = new List<String>(this.tempSentence.Split('.'));
                //Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal,
                //() =>
                //{
                    this.formRealtimeConversation();
                //}
                //);

            }
            else
            {
                // tempSentence can be set regardless
                this.tempSentence = this.constructTempSentence() + json.result.hypotheses[0].transcript.Trim();
                this.realtimeSentences = new List<String>(this.constructTempSentence().Split('.'));
                this.realtimeLastSentence = json.result.hypotheses[0].transcript.Trim();

                Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal,
                () =>
                {
                    this.realtimeConversation.UpdateLastMessage(this.constructRealtimeTempBubble(), true);
                }
                );
            }

            /*
            Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal,
            () =>
            {
                //this.conversation.UpdateLastMessage(this.constructTempBubble(), true);
            }
            );*/
            

            //this.tempSentence = json.result.hypotheses[0].transcript.Trim();

            // Then check if we have results from diarization
            if (json.result.diarization != null && json.result.diarization.Count > 0)
            {
                if (!json.result.diarization_incremental)
                {
                    Debug.WriteLine("Received new diarization. Removing previous results.");
                    this.diarization.Clear();
                }
                foreach (var d in json.result.diarization)
                {
                    int speaker = d.speaker;
                    //Debug.WriteLine("Identified speaker is " + speaker.ToString());
                    double start = d.start;
                    double end = d.end;

                    var interval = new TimeInterval(start, end);
                    this.insertToDiarization(interval, speaker);
                    //this.verifyDiarizationInterval();
                }
                
                this.diarizationWordIndex = this.assignSpeakerToWord();
                //Debug.WriteLine("Diarized to word count" + diarizationWordIndex.ToString());
                //Debug.WriteLine("Total word count" + words.Count.ToString());
                //Debug.WriteLine(json.original);
                this.formConversation();
                
                // so that we don't have an overflow of words
                this.constructTempSentence();
                this.printDiarizationResult();
            }
            
            // latest sentence has a length cap
            this.constructLatestSentence();
        }

        // Show all words that have not been diarized
        private string constructTempSentence()
        {
            string result = String.Empty;
            for (int i = this.diarizationWordIndex; i < this.words.Count; i++)
            {
                result += " " + words[i].word;
            }

            return result;
        }

        private TextMessage constructTempBubble()
        {
            var message = new TextMessage()
            {
                Body = this.tempSentence,
                Speaker = 99,
                //DisplayTime = DateTime.Now.ToString(),
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
                //Interval = new TimeInterval(start, start + length),
                IsFinal = false,
                ConversationIndex = this.conversationIndex
        };

            return message;
        }

        private void constructLatestSentence()
        {
            int longSentenceCap = 80;

            string[] a = this.tempSentence.Split(new char[] { '.' }, StringSplitOptions.RemoveEmptyEntries);    // need to remove the empty one after '.'
            this.latestSentence = a[a.Count() - 1];

            if (this.latestSentence.Length > longSentenceCap)
            {
                this.latestSentence = this.latestSentence.Substring(this.latestSentence.Length - longSentenceCap, longSentenceCap);
                this.latestSentence = "... " + this.latestSentence;
            }
        }

        // Label speaker for each word according to speaker intervals
        // returns index to word that has been diarized
        private int assignSpeakerToWord()
        {
            
            int di = 0;
            int wi = 0;
            int prevAssigned = 0;
           
            for (wi = 0; wi < words.Count; wi++)
            {
                bool assigned = false;
                int bounded = -1;
                for (di = 0; di < diarization.Count; di++)
                {
                    double word_middle = (words[wi].interval.start + words[wi].interval.end) / 2;
                    if (word_middle < diarization[di].first.end)
                    {
                        words[wi].speaker = diarization[di].second;
                        assigned = true;
                        break;
                    }
                    if (words[wi].interval.end < diarization[di].first.end)
                    {
                        bounded = diarization[di].second;
                        break;
                    }
                }
                if (!assigned && bounded == -1)
                {
                    break;
                }
            }

            return wi;
        }

        

        // Concatenate words together to form sentences
        // awkward thing is we don't know how to get sentences
        private List<TextMessage> formConversation()
        {
            if (words.Count == 0)
            {
                return new List<TextMessage>();
            }

            List<TextMessage> messages = new List<TextMessage>();

            int prevSpeaker = words[0].speaker;
            double prevStart = 0;
            double speechEnd = 0;
            string sentence = String.Empty;

            int i = 0;

            for (i = 0; i < words.Count; i++)
            {
                // We display a new sentence when we detect a new speaker or that the speech engine thinks an sentence has ended
                 // and that the sentence is longer than 50 characters.
                if ((words[i].speaker != prevSpeaker && sentence.Length != 0) || (words[i].word == "." && sentence.Length > 50))
                {
                    var message = new TextMessage()
                    {
                        Body = sentence + ".",
                        Speaker = (uint)prevSpeaker,
                        //DisplayTime = format_seconds(prevStart, words[i-1].interval.end),
                        Interval = new TimeInterval(prevStart, words[i - 1].interval.end),
                        IsFinal = true,
                        ConversationIndex = this.conversationIndex
                    };
                    messages.Add(message);
                    prevSpeaker = words[i].speaker;
                    sentence = String.Empty;

                    prevStart = words[i].interval.start;
                }

                if (words[i].word != ".")
                {
                    sentence += " " + words[i].word;
                    speechEnd = words[i].interval.end;
                }

                // Words beyong this point have not been diarized yet
                if (words[i].speaker == -1)
                {
                    break;
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
                    ConversationIndex = this.conversationIndex
                };
                messages.Add(m);

            }

            //TextMessage[] oldMessages = new TextMessage[oldConversations.Count];
            //oldConversations.CopyTo(oldMessages);
            List<TextMessage> temp = new List<TextMessage>(oldConversations);
            temp.AddRange(messages);

            Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal,
            () =>
            {
                //this.conversation.Clear();
                this.conversation.ClearThenAddRange(temp);
                //this.conversation.UpdateLastMessage(this.constructTempBubble(), false);
            } 
            );


            return messages;
        }

        private void formRealtimeConversation()
        {
            List<TextMessage> messages = new List<TextMessage>();

            foreach(string sentence in this.realtimeSentences)
            {
                if (sentence.Length > 0)
                {
                    var m = new TextMessage()
                    {
                        Body = sentence,
                        Speaker = (uint) 99,
                        //DisplayTime = DateTime.Now.ToString(),
                        IsFinal = true,
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
                this.realtimeConversation.UpdateLastMessage(this.constructRealtimeTempBubble(), false);
            }
            );

        }

        // Scan through the all diarization we have completed to find if things need to be updated
        private void insertToDiarization(TimeInterval interval, int speaker)
        {
            bool operated = false;
            for (var i = 0; i < diarization.Count; i++)
            {
                if (diarization[i].first.end > interval.start && diarization[i].first.end < interval.end)
                {
                    // same speaker, merge interval
                    if (diarization[i].second == speaker)
                    {
                        diarization[i].first.end = interval.end;
                    }
                    else
                    {
                        diarization[i].first.end = interval.start;

                        // later diarizations should be more accurate
                        diarization.Insert(i + 1, new Pair<TimeInterval, int>(interval, speaker));
                    }

                    operated = true;
                    break;
                }
            }

            if (operated == false)
            {
                diarization.Add(new Pair<TimeInterval, int>(interval, speaker));
            }
        }

        // We check diarization list itself to make sure there is no overlapping intervals
        // too lazy to write check for partially overlapping interval
        private void verifyDiarizationInterval()
        {
            bool check = true;

            while (check)
            {
                bool operated = false;
                for (var i = 0; i < diarization.Count; i++)
                {
                    operated = false;
                    for (var j = i + 1; j < diarization.Count; j++)
                    {
                        if (diarization[i].first.start <= diarization[j].first.start && diarization[i].first.end >= diarization[j].first.end)
                        {
                            diarization.RemoveAt(j);
                            operated = true;
                            break;
                        }
                        else if (diarization[i].first.start >= diarization[j].first.start && diarization[i].first.end <= diarization[j].first.end)
                        {
                            diarization.RemoveAt(i);
                            operated = true;
                            break;
                        }
                    }

                    if (operated)
                    {
                        break;
                    }
                }

                if (operated == false)
                {
                    check = false;
                }
            }

        }

        public void printDiarizationResult()
        {
            for (int i = 0; i < diarization.Count; i++)
            {
                Debug.WriteLine("Start: " + diarization[i].first.start.ToString() + " End: " + diarization[i].first.end.ToString() + " => Speaker " + diarization[i].second.ToString());
            }

            for (int i = 0; i < words.Count; i++)
            {
                Debug.WriteLine("[" + i.ToString() + "] " + words[i].word + " Start: " + words[i].interval.start + " => Speaker " + words[i].speaker.ToString());
            }

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
    }
}
