#pragma once
#include <cstdint>
#include <functional>

class APU2A03 {
public:
  APU2A03();
  ~APU2A03();

  void cpuWrite(uint16_t addr, uint8_t data);
  uint8_t cpuRead(uint16_t addr);
  void clock();
  void reset();

  // Get current audio sample (-1.0 to 1.0)
  double GetOutputSample();

  // Connect memory read callback for DMC
  void SetReadCallback(std::function<uint8_t(uint16_t)> readFunc) {
    cpuReadCallback = readFunc;
  }

  // IRQ status
  bool GetIRQ() { return frameIRQ || dmcIRQ; }
  void ClearFrameIRQ() { frameIRQ = false; }

private:
  // Memory read callback for DMC
  std::function<uint8_t(uint16_t)> cpuReadCallback;

  // Clock counters
  uint64_t clockCounter = 0;

  // Frame counter
  bool frameCounterMode = false; // false = 4-step, true = 5-step
  bool frameIRQInhibit = false;
  bool frameIRQ = false;
  uint16_t frameCounter = 0;

  // Lookup tables
  static const uint8_t lengthTable[32];
  static const uint8_t dutyTable[4][8];
  static const uint8_t triangleTable[32];
  static const uint16_t noiseTimerTable[16];
  static const uint16_t dmcRateTable[16];

  //========================================
  // PULSE CHANNEL (shared structure for 1 and 2)
  //========================================
  struct PulseChannel {
    // Registers
    bool enabled = false;
    uint8_t duty = 0;
    bool lengthHalt = false; // Also envelope loop
    bool constantVolume = false;
    uint8_t envelopePeriod = 0;

    bool sweepEnabled = false;
    uint8_t sweepPeriod = 0;
    bool sweepNegate = false;
    uint8_t sweepShift = 0;

    uint16_t timerPeriod = 0;
    uint8_t lengthCounter = 0;

    // Internal state
    uint16_t timerValue = 0;
    uint8_t dutyPosition = 0;

    bool envelopeStart = false;
    uint8_t envelopeDivider = 0;
    uint8_t envelopeDecay = 0;

    bool sweepReload = false;
    uint8_t sweepDivider = 0;
    uint16_t sweepTargetPeriod = 0;
    bool sweepMuting = false;

    void clockTimer();
    void clockEnvelope();
    void clockLengthCounter();
    void clockSweep(bool isChannel1);
    void updateSweepTargetPeriod(bool isChannel1);
    uint8_t output();
  };

  PulseChannel pulse1;
  PulseChannel pulse2;

  //========================================
  // TRIANGLE CHANNEL
  //========================================
  struct TriangleChannel {
    bool enabled = false;
    bool lengthHalt = false; // Also linear counter control
    uint8_t linearCounterReload = 0;
    uint16_t timerPeriod = 0;
    uint8_t lengthCounter = 0;

    uint16_t timerValue = 0;
    uint8_t sequencerPosition = 0;
    uint8_t linearCounter = 0;
    bool linearCounterReloadFlag = false;

    void clockTimer();
    void clockLinearCounter();
    void clockLengthCounter();
    uint8_t output();
  };

  TriangleChannel triangle;

  //========================================
  // NOISE CHANNEL
  //========================================
  struct NoiseChannel {
    bool enabled = false;
    bool lengthHalt = false;
    bool constantVolume = false;
    uint8_t envelopePeriod = 0;
    bool mode = false; // Short mode
    uint8_t noisePeriod = 0;
    uint8_t lengthCounter = 0;

    uint16_t timerValue = 0;
    uint16_t shiftRegister = 1; // Initial value is 1

    bool envelopeStart = false;
    uint8_t envelopeDivider = 0;
    uint8_t envelopeDecay = 0;

    void clockTimer();
    void clockEnvelope();
    void clockLengthCounter();
    uint8_t output();
  };

  NoiseChannel noise;

  //========================================
  // DMC CHANNEL
  //========================================
  struct DMCChannel {
    bool enabled = false;
    bool irqEnabled = false;
    bool loop = false;
    uint8_t rateIndex = 0;
    uint8_t directLoad = 0;
    uint16_t sampleAddress = 0;
    uint16_t sampleLength = 0;

    // Output
    uint8_t outputLevel = 0;

    // Memory reader
    uint16_t currentAddress = 0;
    uint16_t bytesRemaining = 0;
    uint8_t sampleBuffer = 0;
    bool sampleBufferEmpty = true;

    // Output unit
    uint8_t shiftRegister = 0;
    uint8_t bitsRemaining = 0;
    bool silenceFlag = true;

    // Timer
    uint16_t timerValue = 0;
    uint16_t timerPeriod = 0;

    bool irqFlag = false;

    void clockTimer(std::function<uint8_t(uint16_t)> &readCallback,
                    bool &irqOut);
    void restart();
    uint8_t output();
  };

  DMCChannel dmc;
  bool dmcIRQ = false;

  // Frame counter clocking
  void clockQuarterFrame();
  void clockHalfFrame();
};
