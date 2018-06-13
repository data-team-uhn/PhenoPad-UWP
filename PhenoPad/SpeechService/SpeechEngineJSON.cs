using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PhenoPad.SpeechService
{

    /*
        Here is an example of the JSON string we expect
        We should replace "-" in names to "_" for parsing purpose
        {
            "status": 0, 
            "segment-start": 4.25, 
            "segment-length": 1.26, 
            "total-length": 5.75, 
            "result": {
                "hypotheses": [
                    {
                        "transcript": "hello.", 
                        "confidence": 0.897120821134181, 
                        "likelihood": 31.8709, 
                        "word-alignment": [
                            {
                                "start": 0.48, 
                                "confidence": 0.796075, 
                                "word": "hello", 
                                "length": 0.27
                            }
                        ]
                    }
                'diarization': [
                    {
                        'length': 22.5,
                        'speaker': 1,
                        'start': 22.1
                    },
                    {
                        'length': 22.7,
                        'speaker': 0,
                        'start': 22.6
                    },
                ], 
                "final": true
                "diarization_incremental": true
            }, 
            "segment": 2, 
            "id": "855626f9-d877-4a01-8677-50992ff0bd45"
        }
    */

    /// <summary>
    /// {'start': 7.328, 'speaker': 0, 'end': 9.168000000000001, 'angle': 152.97781134625265}
    /// </summary>
    public class DiarizationJSON
    {
        public double start { get; set; }
        public int speaker { get; set; }
        public double end { get; set; }
        public double angle { get; set; }
        
    }
    public class SpeechEngineJSON
    {
        public int status { get; set; }
        public double segment_start { get; set; }
        public double segment_length { get; set; }
        public double total_length { get; set; }
        public Result result { get; set; }
        public int segment { get; set; }
        public string id { get; set; }
        
        public string original { get; set; }

        public int worker_pid { get; set; }

        // A simplified version for debugging
        public override string ToString()
        {
            string output = "Worker PID: " + this.worker_pid + " ";
            if (result.final)
            {
                output += "Final\t";
            }
            else
            {
                output += "Temp\t";
            }
            output += "(" + segment_start.ToString() + " -> " + (segment_start + segment_length).ToString() + ") \n";
            /**
            if (result.diarization != null && result.diarization.Count > 0)
            {
                output += "Diarizations: " + (result.diarization.Count).ToString() + " \n";
            }
            **/
            
            output += result.hypotheses[0].transcript;
            
            return output;
        }
    }

    public class WordAlignment
    {
        public double start { get; set; }
        public double confidence { get; set; }
        public string word { get; set; }
        public double length { get; set; }
    }

    public class Hypothesis
    {
        public string transcript { get; set; }
        public double confidence { get; set; }
        public double likelihood { get; set; }
        public List<WordAlignment> word_alignment { get; set; }
    }

    public class Diarization
    {
        public double end { get; set; }
        public int speaker { get; set; }
        public double start { get; set; }
    }

    public class Result
    {
        public List<Hypothesis> hypotheses { get; set; }
        public bool final { get; set; }
        public bool diarization_incremental { get; set; }
        public List<Diarization> diarization { get; set; }
    }

    
}
