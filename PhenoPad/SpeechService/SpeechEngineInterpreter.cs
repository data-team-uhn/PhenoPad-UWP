using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using Windows.UI.Core;

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

    class TimeInterval
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
        // Empty constructor :D
        public SpeechEngineInterpreter(Conversation _conv, Conversation _realtimeconv)
        {
            this.conversation = _conv;
            this.realtimeConversation = _realtimeconv;
        }

        // Looks at speech engine result to identify what can be done
        public void processJSON(SpeechEngineJSON json)
        {
            // We take for granted that diarizatio will always be a lot slower than 
            // speech recognition
            
            // First check if speech is final (remember that diarization is always slower)
            if (json.result.final)
            {
                // Then we should have a bunch of words to look at
                foreach (WordAlignment wa in json.result.hypotheses[0].word_alignment)
                {
                    // Remove <laughter> <unk> etc.
                    if (wa.word.IndexOf('[') == -1 && wa.word.IndexOf('<') == -1)
                    {
                        var w = new WordSpoken(wa.word, -1, new TimeInterval(wa.start + json.segment_start, wa.start + wa.length + json.segment_start));
                        words.Add(w);
                    }
                }
                words.Add(new WordSpoken(".", -1, new TimeInterval(0, 0)));

                // because transcrip is final and has already been accounted for
                this.tempSentence = this.constructTempSentence();
                this.realtimeSentences = new List<String>(this.tempSentence.Split('.'));
                Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal,
                () =>
                {
                    this.formRealtimeConversation();
                }
                );
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

            Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal,
            () =>
            {
                //this.conversation.UpdateLastMessage(this.constructTempBubble(), true);
            }
            );
            

            //this.tempSentence = json.result.hypotheses[0].transcript.Trim();

            // Then check if we have results from diarization
            if (json.result.diarization != null && json.result.diarization.Count > 0)
            {
                foreach (var d in json.result.diarization)
                {
                    int speaker = d.speaker;
                    //Debug.WriteLine("Identified speaker is " + speaker.ToString());
                    double start = d.start;
                    double end = d.end;

                    var interval = new TimeInterval(start, end);
                    this.insertToDiarization(interval, speaker);
                    this.verifyDiarizationInterval();
                }
                
                this.diarizationWordIndex = this.assignSpeakerToWord();
                Debug.WriteLine("Diarized to word count" + diarizationWordIndex.ToString());
                Debug.WriteLine("Total word count" + words.Count.ToString());
                Debug.WriteLine(json.original);
                this.formConversation();
                
                // so that we don't have an overflow of words
                this.constructTempSentence();
                //this.printDiarizationResult();
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
                IsFinal = false
            };

            return message;
        }
        private TextMessage constructRealtimeTempBubble()
        {
            var message = new TextMessage()
            {
                Body = this.realtimeLastSentence,
                Speaker = 99,
                //DisplayTime = DateTime.Now.ToString(),
                IsFinal = false
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
            while (di < diarization.Count)
            {
                //Debug.WriteLine("Start: " + diarization[di].first.start.ToString() + " End: " + diarization[di].first.end.ToString() + " => Speaker " + diarization[di].second.ToString());
                for (int i = wi; i < words.Count; i++)
                {
                    if (words[i].interval.start < diarization[di].first.end)
                    {
                        words[i].speaker = diarization[di].second;
                        //Debug.WriteLine("Word[" + i.ToString() + "] Start: " + words[i].interval.start + " => Speaker " + diarization[di].second.ToString());
                    }
                    else
                    {
                        //Debug.WriteLine("interval[" + di.ToString() + "] => Speaker " + diarization[di].second.ToString() + " Word Index: " + wi.ToString());
                        wi = i;
                        break;
                    }
                }
                di++;
            }

            return wi;
        }

        // Concatenate words together to form sentences
        // awkward thing is we don't know how to get sentences
        
        private void formConversation()
        {
            List<TextMessage> messages = new List<TextMessage>();

            int prevSpeaker = words[0].speaker;
            string sentence = String.Empty;

            for (int i = 0; i < words.Count; i++)
            {
                // We display a new sentence when we detect a new speaker or that the speech engine thinks an sentence has ended
                 // and that the sentence is longer than 50 characters.
                if ((words[i].speaker != prevSpeaker && sentence.Length != 0) || (words[i].word == "." && sentence.Length > 50))
                {
                    var message = new TextMessage()
                    {
                        Body = sentence + ".",
                        Speaker = (uint)prevSpeaker,
                        //DisplayTime = DateTime.Now.ToString(),
                        IsFinal = true
                    };
                    messages.Add(message);
                    prevSpeaker = words[i].speaker;
                    sentence = String.Empty;
                }

                if (words[i].word != ".")
                {
                    sentence += " " + words[i].word;
                }

                // Words beyong this point have not been diarized yet
                if (words[i].speaker == -1)
                {
                    break;
                }
            }

            // Do not add new sentence if it is empty
            if (sentence.Length > 0)
            {
                var m = new TextMessage()
                {
                    Body = sentence,
                    Speaker = (uint)prevSpeaker,
                    //DisplayTime = DateTime.Now.ToString(),
                    IsFinal = true
                };
                messages.Add(m);

            }

            Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal,
            () =>
            {
                //this.conversation.Clear();
                this.conversation.ClearThenAddRange(messages);
                //this.conversation.UpdateLastMessage(this.constructTempBubble(), false);
            } 
            );
            
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
                        IsFinal = true
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
                Debug.WriteLine((content.Length).ToString() + ", " + (index).ToString());
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
