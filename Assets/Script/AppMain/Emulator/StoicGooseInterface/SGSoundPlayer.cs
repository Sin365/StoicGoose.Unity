using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

public class SGSoundPlayer : MonoBehaviour//, ISoundPlayer
{
    [SerializeField]
    private AudioSource m_as;
    private RingBuffer<float> _buffer = new RingBuffer<float>(44100 * 2);
    private TimeSpan lastElapsed;
    public double audioFPS { get; private set; }
    public bool IsRecording { get; private set; }

    void Awake()
    {
        // ��ȡ��ǰ��Ƶ����
        AudioConfiguration config = AudioSettings.GetConfiguration();

        // ����Ŀ����Ƶ����
        config.sampleRate = 44100;       // ������Ϊ 44100Hz
        config.numRealVoices = 32;      // ���������ƵԴ��������ѡ��
        config.numVirtualVoices = 512;   // ����������ƵԴ��������ѡ��
        config.dspBufferSize = 1024;     // ���� DSP ��������С����ѡ��
        config.speakerMode = AudioSpeakerMode.Stereo; // ����Ϊ��������2 ������

        // Ӧ���µ���Ƶ����
        if (AudioSettings.Reset(config))
        {
            Debug.Log("Audio settings updated successfully.");
            Debug.Log("Sample Rate: " + config.sampleRate + "Hz");
            Debug.Log("Speaker Mode: " + config.speakerMode);
        }
        else
        {
            Debug.LogError("Failed to update audio settings.");
        }

    }

    private Queue<float> sampleQueue = new Queue<float>();


    // Unity ��Ƶ�̻߳ص�
    void OnAudioFilterRead(float[] data, int channels)
    {
        for (int i = 0; i < data.Length; i++)
        {
            if (_buffer.TryRead(out float rawData))
                data[i] = rawData;
            else
                data[i] = 0; // ������ʱ����
        }
    }


    public void Initialize()
    {
        if (!m_as.isPlaying)
        {
            m_as.Play();
        }
    }

    public void StopPlay()
    {
        if (m_as.isPlaying)
        {
            m_as.Stop();
        }
    }

    public void SubmitSamples(short[] buffer, short[][] ChannelSamples, int samples_a)
    {
        var current = UStoicGoose.sw.Elapsed;
        var delta = current - lastElapsed;
        lastElapsed = current;
        audioFPS = 1d / delta.TotalSeconds;

        for (int i = 0; i < samples_a; i += 1)
        {
            _buffer.Write(buffer[i] / 32767.0f);

        }
        if (IsRecording)
        {
            dataChunk.AddSampleData(buffer, samples_a);
            waveHeader.FileLength += (uint)samples_a;
        }
    }
    public void BufferWirte(int Off, byte[] Data)
    {
    }

    public void GetCurrentPosition(out int play_position, out int write_position)
    {
        play_position = 0;
        write_position = 0;
    }

    public void SetVolume(int Vol)
    {
        //TODO ����
        if (m_as)
            return;
        m_as.volume = Vol;
    }


    void Update()
    {
        if (Input.GetKeyDown(KeyCode.F3))
        {
            BeginRecording();
            Debug.Log("¼��");
        }
        if (Input.GetKeyDown(KeyCode.F4))
        {
            SaveRecording("D:/1.wav");
            Debug.Log("����");
        }
    }
    WaveHeader waveHeader;
    FormatChunk formatChunk;
    DataChunk dataChunk;
    public void BeginRecording()
    {
        waveHeader = new WaveHeader();
        formatChunk = new FormatChunk(44100, 2);
        dataChunk = new DataChunk();
        waveHeader.FileLength += formatChunk.Length();

        IsRecording = true;

    }


    public void SaveRecording(string filename)
    {
        using (FileStream file = new FileStream(filename, FileMode.Create, FileAccess.Write, FileShare.ReadWrite))
        {
            file.Write(waveHeader.GetBytes(), 0, (int)waveHeader.Length());
            file.Write(formatChunk.GetBytes(), 0, (int)formatChunk.Length());
            file.Write(dataChunk.GetBytes(), 0, (int)dataChunk.Length());
        }

        IsRecording = false;

    }

    internal void EnqueueSamples(short[] s)
    {
        throw new NotImplementedException();
    }

    internal void Unpause()
    {
        throw new NotImplementedException();
    }

    internal void Pause()
    {
        throw new NotImplementedException();
    }

    class WaveHeader
    {
        const string fileTypeId = "RIFF";
        const string mediaTypeId = "WAVE";

        public string FileTypeId { get; private set; }
        public uint FileLength { get; set; }
        public string MediaTypeId { get; private set; }

        public WaveHeader()
        {
            FileTypeId = fileTypeId;
            MediaTypeId = mediaTypeId;
            FileLength = 4;     /* Minimum size is always 4 bytes */
        }

        public byte[] GetBytes()
        {
            List<byte> chunkData = new List<byte>();

            chunkData.AddRange(Encoding.ASCII.GetBytes(FileTypeId));
            chunkData.AddRange(BitConverter.GetBytes(FileLength));
            chunkData.AddRange(Encoding.ASCII.GetBytes(MediaTypeId));

            return chunkData.ToArray();
        }

        public uint Length()
        {
            return (uint)GetBytes().Length;
        }
    }

    class FormatChunk
    {
        const string chunkId = "fmt ";

        ushort bitsPerSample, channels;
        uint frequency;

        public string ChunkId { get; private set; }
        public uint ChunkSize { get; private set; }
        public ushort FormatTag { get; private set; }

        public ushort Channels
        {
            get { return channels; }
            set { channels = value; RecalcBlockSizes(); }
        }

        public uint Frequency
        {
            get { return frequency; }
            set { frequency = value; RecalcBlockSizes(); }
        }

        public uint AverageBytesPerSec { get; private set; }
        public ushort BlockAlign { get; private set; }

        public ushort BitsPerSample
        {
            get { return bitsPerSample; }
            set { bitsPerSample = value; RecalcBlockSizes(); }
        }

        public FormatChunk()
        {
            ChunkId = chunkId;
            ChunkSize = 16;
            FormatTag = 1;          /* MS PCM (Uncompressed wave file) */
            Channels = 2;           /* Default to stereo */
            Frequency = 44100;      /* Default to 44100hz */
            BitsPerSample = 16;     /* Default to 16bits */
            RecalcBlockSizes();
        }

        public FormatChunk(int frequency, int channels) : this()
        {
            Channels = (ushort)channels;
            Frequency = (ushort)frequency;
            RecalcBlockSizes();
        }

        private void RecalcBlockSizes()
        {
            BlockAlign = (ushort)(channels * (bitsPerSample / 8));
            AverageBytesPerSec = frequency * BlockAlign;
        }

        public byte[] GetBytes()
        {
            List<byte> chunkBytes = new List<byte>();

            chunkBytes.AddRange(Encoding.ASCII.GetBytes(ChunkId));
            chunkBytes.AddRange(BitConverter.GetBytes(ChunkSize));
            chunkBytes.AddRange(BitConverter.GetBytes(FormatTag));
            chunkBytes.AddRange(BitConverter.GetBytes(Channels));
            chunkBytes.AddRange(BitConverter.GetBytes(Frequency));
            chunkBytes.AddRange(BitConverter.GetBytes(AverageBytesPerSec));
            chunkBytes.AddRange(BitConverter.GetBytes(BlockAlign));
            chunkBytes.AddRange(BitConverter.GetBytes(BitsPerSample));

            return chunkBytes.ToArray();
        }

        public uint Length()
        {
            return (uint)GetBytes().Length;
        }
    }

    class DataChunk
    {
        const string chunkId = "data";

        public string ChunkId { get; private set; }
        public uint ChunkSize { get; set; }
        public List<short> WaveData { get; private set; }

        public DataChunk()
        {
            ChunkId = chunkId;
            ChunkSize = 0;
            WaveData = new List<short>();
        }

        public byte[] GetBytes()
        {
            List<byte> chunkBytes = new List<byte>();

            chunkBytes.AddRange(Encoding.ASCII.GetBytes(ChunkId));
            chunkBytes.AddRange(BitConverter.GetBytes(ChunkSize));
            byte[] bufferBytes = new byte[WaveData.Count * 2];
            Buffer.BlockCopy(WaveData.ToArray(), 0, bufferBytes, 0, bufferBytes.Length);
            chunkBytes.AddRange(bufferBytes.ToList());

            return chunkBytes.ToArray();
        }

        public uint Length()
        {
            return (uint)GetBytes().Length;
        }

        public void AddSampleData(short[] stereoBuffer)
        {
            WaveData.AddRange(stereoBuffer);

            ChunkSize += (uint)(stereoBuffer.Length * 2);
        }
        //public unsafe void AddSampleData(short* stereoBuffer, int lenght)
        //{
        //    for (int i = 0; i < lenght; i++)
        //    {
        //        WaveData.Add(stereoBuffer[i]);
        //    }

        //    ChunkSize += (uint)(lenght * 2);
        //}
    }
}
