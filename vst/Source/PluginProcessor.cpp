#include "PluginProcessor.h"

#include <chrono>

static constexpr const char* kPipeName = "CordCastAudio";

CordCastSendProcessor::CordCastSendProcessor()
    : AudioProcessor(BusesProperties()
        .withInput ("Input",  juce::AudioChannelSet::stereo(), true)
        .withOutput("Output", juce::AudioChannelSet::stereo(), true))
{
}

CordCastSendProcessor::~CordCastSendProcessor()
{
    releaseResources();
}

void CordCastSendProcessor::prepareToPlay(double /*sampleRate*/, int samplesPerBlock)
{
    _sendBuffer.reserve(static_cast<size_t>(samplesPerBlock) * 2);

    bool wasRunning = _running.exchange(true);
    if (!wasRunning)
    {
        if (_connectThread.joinable())
            _connectThread.join();
        _connectThread = std::thread([this] { connectLoop(); });
    }
}

void CordCastSendProcessor::releaseResources()
{
    _running  = false;
    _connected = false;
    _pipe.close();
    if (_connectThread.joinable())
        _connectThread.join();
}

void CordCastSendProcessor::connectLoop()
{
    while (_running)
    {
        if (!_connected)
        {
            if (_pipe.openExisting(kPipeName))
                _connected = true;
        }

        // Sleep in short increments so we notice _running=false quickly.
        for (int i = 0; i < 10 && _running; ++i)
            std::this_thread::sleep_for(std::chrono::milliseconds(100));
    }
}

void CordCastSendProcessor::processBlock(juce::AudioBuffer<float>& buffer,
                                         juce::MidiBuffer& /*midi*/)
{
    if (!_running || !_connected)
        return;

    const int numSamples  = buffer.getNumSamples();
    const int numChannels = juce::jmin(buffer.getNumChannels(), 2);
    const float sampleRate = static_cast<float>(getSampleRate());

    // Header: numSamples (uint32) | sampleRate (float32) | numChannels (uint32)
    uint8_t header[12];
    const uint32_t ns = static_cast<uint32_t>(numSamples);
    const uint32_t nc = static_cast<uint32_t>(numChannels);
    std::memcpy(header + 0, &ns,         4);
    std::memcpy(header + 4, &sampleRate, 4);
    std::memcpy(header + 8, &nc,         4);

    // Interleaved float32 samples
    _sendBuffer.resize(static_cast<size_t>(numSamples * numChannels));
    for (int s = 0; s < numSamples; ++s)
        for (int c = 0; c < numChannels; ++c)
            _sendBuffer[static_cast<size_t>(s * numChannels + c)] = buffer.getSample(c, s);

    const int bodyBytes = numSamples * numChannels * 4;

    // 5 ms write timeout keeps audio thread safe; failure → reconnect.
    if (_pipe.write(header, 12, 5) != 12 ||
        _pipe.write(_sendBuffer.data(), bodyBytes, 5) != bodyBytes)
    {
        _connected = false;
        _pipe.close();
    }
}

juce::AudioProcessor* JUCE_CALLTYPE createPluginFilter()
{
    return new CordCastSendProcessor();
}
