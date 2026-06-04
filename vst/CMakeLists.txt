cmake_minimum_required(VERSION 3.22)
project(CordCastSend VERSION 1.0.0)

set(CMAKE_CXX_STANDARD 17)
set(CMAKE_CXX_STANDARD_REQUIRED ON)

# Windows x64 only
if(NOT WIN32)
    message(FATAL_ERROR "CordCast VST plugin is Windows-only.")
endif()

include(FetchContent)
FetchContent_Declare(
    JUCE
    GIT_REPOSITORY https://github.com/juce-framework/JUCE.git
    GIT_TAG        8.0.4
    GIT_SHALLOW    TRUE
)
FetchContent_MakeAvailable(JUCE)

juce_add_plugin(CordCastSend
    PLUGIN_MANUFACTURER_CODE Ccst
    PLUGIN_CODE Csnd
    FORMATS VST3
    PRODUCT_NAME "CordCast Send"
    COMPANY_NAME "CordCast"
    IS_PLUGIN_FILTER TRUE
    NEEDS_MIDI_INPUT FALSE
    NEEDS_MIDI_OUTPUT FALSE
    IS_SYNTH FALSE
    EDITOR_WANTS_KEYBOARD_FOCUS FALSE
    VST3_CATEGORIES "Fx"
)

target_sources(CordCastSend PRIVATE
    Source/PluginProcessor.cpp
    Source/PluginProcessor.h
)

target_compile_definitions(CordCastSend PUBLIC
    JUCE_WEB_BROWSER=0
    JUCE_USE_CURL=0
    JUCE_VST3_CAN_REPLACE_VST2=0
)

target_link_libraries(CordCastSend
    PRIVATE
        juce::juce_audio_processors
        juce::juce_core
    PUBLIC
        juce::juce_recommended_config_flags
        juce::juce_recommended_warning_flags
)
