#pragma once

#include <juce_audio_processors/juce_audio_processors.h>
#include <juce_core/juce_core.h>

#include <atomic>
#include <thread>
#include <vector>

class CordCastSendProcessor final : public juce::AudioProcessor
{
public:
    CordCastSendProcessor();
    ~CordCastSendProcessor() override;

    void prepareToPlay(double sampleRate, int samplesPerBlock) override;
    void releaseResources() override;

    void processBlock(juce::AudioBuffer<float>&, juce::MidiBuffer&) override;

    // No UI
    juce::AudioProcessorEditor* createEditor() override { return nullptr; }
    bool hasEditor() const override { return false; }

    const juce::String getName() const override { return "CordCast Send"; }
    bool acceptsMidi() const override  { return false; }
    bool producesMidi() const override { return false; }
    double getTailLengthSeconds() const override { return 0.0; }

    int getNumPrograms() override             { return 1; }
    int getCurrentProgram() override          { return 0; }
    void setCurrentProgram(int) override      {}
    const juce::String getProgramName(int) override { return {}; }
    void changeProgramName(int, const juce::String&) override {}

    void getStateInformation(juce::MemoryBlock&) override {}
    void setStateInformation(const void*, int) override   {}

private:
    juce::NamedPipe _pipe;
    std::atomic<bool> _connected { false };
    std::atomic<bool> _running   { false };
    std::thread _connectThread;

    std::vector<float> _sendBuffer;

    void connectLoop();

    JUCE_DECLARE_NON_COPYABLE_WITH_LEAK_DETECTOR(CordCastSendProcessor)
};
