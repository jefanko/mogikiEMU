#include "APU2A03.h"

// Length counter lookup table
const uint8_t APU2A03::lengthTable[32] = {
    10, 254, 20, 2,  40, 4,  80, 6,  160, 8,  60, 10, 14, 12, 26, 14,
    12, 16,  24, 18, 48, 20, 96, 22, 192, 24, 72, 26, 16, 28, 32, 30};

// Duty cycle sequences
const uint8_t APU2A03::dutyTable[4][8] = {
    {0, 0, 0, 0, 0, 0, 0, 1}, // 12.5%
    {0, 0, 0, 0, 0, 0, 1, 1}, // 25%
    {0, 0, 0, 0, 1, 1, 1, 1}, // 50%
    {1, 1, 1, 1, 1, 1, 0, 0}  // 75%
};

// Triangle waveform
const uint8_t APU2A03::triangleTable[32] = {
    15, 14, 13, 12, 11, 10, 9, 8, 7, 6, 5,  4,  3,  2,  1,  0,
    0,  1,  2,  3,  4,  5,  6, 7, 8, 9, 10, 11, 12, 13, 14, 15};

// Noise timer periods (NTSC)
const uint16_t APU2A03::noiseTimerTable[16] = {
    4, 8, 16, 32, 64, 96, 128, 160, 202, 254, 380, 508, 762, 1016, 2034, 4068};

// DMC rate table (NTSC)
const uint16_t APU2A03::dmcRateTable[16] = {428, 380, 340, 320, 286, 254,
                                            226, 214, 190, 160, 142, 128,
                                            106, 84,  72,  54};

APU2A03::APU2A03() { reset(); }

APU2A03::~APU2A03() {}

void APU2A03::reset() {
  clockCounter = 0;
  frameCounter = 0;
  frameCounterMode = false;
  frameIRQInhibit = false;
  frameIRQ = false;

  pulse1 = PulseChannel();
  pulse2 = PulseChannel();
  triangle = TriangleChannel();
  noise = NoiseChannel();
  noise.shiftRegister = 1;
  dmc = DMCChannel();
  dmcIRQ = false;
}

//========================================
// PULSE CHANNEL IMPLEMENTATION
//========================================
void APU2A03::PulseChannel::clockTimer() {
  if (timerValue == 0) {
    timerValue = timerPeriod;
    dutyPosition = (dutyPosition + 1) & 7;
  } else {
    timerValue--;
  }
}

void APU2A03::PulseChannel::clockEnvelope() {
  if (envelopeStart) {
    envelopeStart = false;
    envelopeDecay = 15;
    envelopeDivider = envelopePeriod;
  } else {
    if (envelopeDivider == 0) {
      envelopeDivider = envelopePeriod;
      if (envelopeDecay > 0) {
        envelopeDecay--;
      } else if (lengthHalt) { // lengthHalt doubles as envelope loop
        envelopeDecay = 15;
      }
    } else {
      envelopeDivider--;
    }
  }
}

void APU2A03::PulseChannel::clockLengthCounter() {
  if (lengthCounter > 0 && !lengthHalt) {
    lengthCounter--;
  }
}

void APU2A03::PulseChannel::updateSweepTargetPeriod(bool isChannel1) {
  uint16_t delta = timerPeriod >> sweepShift;
  if (sweepNegate) {
    sweepTargetPeriod = timerPeriod - delta;
    if (isChannel1) {
      sweepTargetPeriod--; // Pulse 1 uses one's complement
    }
  } else {
    sweepTargetPeriod = timerPeriod + delta;
  }

  // Muting conditions:
  // 1. Current period too low (< 8)
  // 2. Target period too high (> 0x7FF) - only when NOT negating
  sweepMuting = (timerPeriod < 8);
  if (!sweepNegate && sweepTargetPeriod > 0x7FF) {
    sweepMuting = true;
  }
}

void APU2A03::PulseChannel::clockSweep(bool isChannel1) {
  updateSweepTargetPeriod(isChannel1);

  if (sweepDivider == 0 && sweepEnabled && !sweepMuting && sweepShift > 0) {
    timerPeriod = sweepTargetPeriod;
  }

  if (sweepDivider == 0 || sweepReload) {
    sweepDivider = sweepPeriod;
    sweepReload = false;
  } else {
    sweepDivider--;
  }
}

uint8_t APU2A03::PulseChannel::output() {
  if (!enabled)
    return 0;
  if (lengthCounter == 0)
    return 0;
  if (sweepMuting)
    return 0;
  if (dutyTable[duty][dutyPosition] == 0)
    return 0;

  return constantVolume ? envelopePeriod : envelopeDecay;
}

//========================================
// TRIANGLE CHANNEL IMPLEMENTATION
//========================================
void APU2A03::TriangleChannel::clockTimer() {
  if (timerValue == 0) {
    timerValue = timerPeriod;
    if (lengthCounter > 0 && linearCounter > 0) {
      sequencerPosition = (sequencerPosition + 1) & 31;
    }
  } else {
    timerValue--;
  }
}

void APU2A03::TriangleChannel::clockLinearCounter() {
  if (linearCounterReloadFlag) {
    linearCounter = linearCounterReload;
  } else if (linearCounter > 0) {
    linearCounter--;
  }

  if (!lengthHalt) {
    linearCounterReloadFlag = false;
  }
}

void APU2A03::TriangleChannel::clockLengthCounter() {
  if (lengthCounter > 0 && !lengthHalt) {
    lengthCounter--;
  }
}

uint8_t APU2A03::TriangleChannel::output() {
  if (!enabled)
    return 0;
  if (lengthCounter == 0)
    return 0;
  if (linearCounter == 0)
    return 0;
  // Ultrasonic frequencies - mute to avoid popping
  if (timerPeriod < 2)
    return 7;

  return triangleTable[sequencerPosition];
}

//========================================
// NOISE CHANNEL IMPLEMENTATION
//========================================
void APU2A03::NoiseChannel::clockTimer() {
  if (timerValue == 0) {
    timerValue = noiseTimerTable[noisePeriod];

    // Feedback bit
    uint8_t bit = mode ? 6 : 1;
    uint16_t feedback = (shiftRegister & 1) ^ ((shiftRegister >> bit) & 1);
    shiftRegister = (shiftRegister >> 1) | (feedback << 14);
  } else {
    timerValue--;
  }
}

void APU2A03::NoiseChannel::clockEnvelope() {
  if (envelopeStart) {
    envelopeStart = false;
    envelopeDecay = 15;
    envelopeDivider = envelopePeriod;
  } else {
    if (envelopeDivider == 0) {
      envelopeDivider = envelopePeriod;
      if (envelopeDecay > 0) {
        envelopeDecay--;
      } else if (lengthHalt) {
        envelopeDecay = 15;
      }
    } else {
      envelopeDivider--;
    }
  }
}

void APU2A03::NoiseChannel::clockLengthCounter() {
  if (lengthCounter > 0 && !lengthHalt) {
    lengthCounter--;
  }
}

uint8_t APU2A03::NoiseChannel::output() {
  if (!enabled)
    return 0;
  if (lengthCounter == 0)
    return 0;
  if (shiftRegister & 1)
    return 0;

  return constantVolume ? envelopePeriod : envelopeDecay;
}

//========================================
// DMC CHANNEL IMPLEMENTATION
//========================================
void APU2A03::DMCChannel::clockTimer(
    std::function<uint8_t(uint16_t)> &readCallback, bool &irqOut) {
  if (timerValue == 0) {
    timerValue = timerPeriod;

    if (!silenceFlag) {
      if (shiftRegister & 1) {
        if (outputLevel <= 125)
          outputLevel += 2;
      } else {
        if (outputLevel >= 2)
          outputLevel -= 2;
      }
      shiftRegister >>= 1;
    }

    bitsRemaining--;
    if (bitsRemaining == 0) {
      bitsRemaining = 8;
      if (sampleBufferEmpty) {
        silenceFlag = true;
      } else {
        silenceFlag = false;
        shiftRegister = sampleBuffer;
        sampleBufferEmpty = true;
      }
    }

    // Fill sample buffer if empty
    if (sampleBufferEmpty && bytesRemaining > 0 && readCallback) {
      sampleBuffer = readCallback(currentAddress);
      sampleBufferEmpty = false;

      currentAddress++;
      if (currentAddress == 0)
        currentAddress = 0x8000;

      bytesRemaining--;
      if (bytesRemaining == 0) {
        if (loop) {
          restart();
        } else if (irqEnabled) {
          irqFlag = true;
          irqOut = true;
        }
      }
    }
  } else {
    timerValue--;
  }
}

void APU2A03::DMCChannel::restart() {
  currentAddress = sampleAddress;
  bytesRemaining = sampleLength;
}

uint8_t APU2A03::DMCChannel::output() { return outputLevel; }

//========================================
// FRAME COUNTER
//========================================
void APU2A03::clockQuarterFrame() {
  pulse1.clockEnvelope();
  pulse2.clockEnvelope();
  triangle.clockLinearCounter();
  noise.clockEnvelope();
}

void APU2A03::clockHalfFrame() {
  pulse1.clockLengthCounter();
  pulse1.clockSweep(true);
  pulse2.clockLengthCounter();
  pulse2.clockSweep(false);
  triangle.clockLengthCounter();
  noise.clockLengthCounter();
}

//========================================
// MAIN CLOCK
//========================================
void APU2A03::clock() {
  // Triangle clocks every CPU cycle
  triangle.clockTimer();

  // Other channels clock every other CPU cycle
  if (clockCounter % 2 == 0) {
    pulse1.clockTimer();
    pulse2.clockTimer();
    noise.clockTimer();
  }

  // DMC clocks every CPU cycle
  dmc.clockTimer(cpuReadCallback, dmcIRQ);

  // Frame counter
  // APU frame counter runs at ~240 Hz
  // CPU runs at 1789773 Hz, so frame counter steps at specific cycles

  if (!frameCounterMode) {
    // 4-step sequence (mode 0)
    // Steps at cycles: 7457, 14913, 22371, 29829 (relative to frame start)
    // We'll use values based on full CPU cycles now
    if (frameCounter == 7457) {
      clockQuarterFrame();
    } else if (frameCounter == 14913) {
      clockQuarterFrame();
      clockHalfFrame();
    } else if (frameCounter == 22371) {
      clockQuarterFrame();
    } else if (frameCounter == 29829) {
      clockQuarterFrame();
      clockHalfFrame();
      if (!frameIRQInhibit) {
        frameIRQ = true;
      }
      frameCounter = 0;
    }
  } else {
    // 5-step sequence (mode 1)
    if (frameCounter == 7457) {
      clockQuarterFrame();
    } else if (frameCounter == 14913) {
      clockQuarterFrame();
      clockHalfFrame();
    } else if (frameCounter == 22371) {
      clockQuarterFrame();
    } else if (frameCounter == 29829) {
      // Nothing happens on step 4 in mode 1
    } else if (frameCounter == 37281) {
      clockQuarterFrame();
      clockHalfFrame();
      frameCounter = 0;
    }
  }

  frameCounter++;
  clockCounter++;
}

//========================================
// CPU INTERFACE
//========================================
void APU2A03::cpuWrite(uint16_t addr, uint8_t data) {
  switch (addr) {
  // Pulse 1
  case 0x4000:
    pulse1.duty = (data >> 6) & 0x03;
    pulse1.lengthHalt = data & 0x20;
    pulse1.constantVolume = data & 0x10;
    pulse1.envelopePeriod = data & 0x0F;
    break;
  case 0x4001:
    pulse1.sweepEnabled = data & 0x80;
    pulse1.sweepPeriod = (data >> 4) & 0x07;
    pulse1.sweepNegate = data & 0x08;
    pulse1.sweepShift = data & 0x07;
    pulse1.sweepReload = true;
    break;
  case 0x4002:
    pulse1.timerPeriod = (pulse1.timerPeriod & 0xFF00) | data;
    break;
  case 0x4003:
    pulse1.timerPeriod = (pulse1.timerPeriod & 0x00FF) | ((data & 0x07) << 8);
    pulse1.timerValue = pulse1.timerPeriod;
    if (pulse1.enabled) {
      pulse1.lengthCounter = lengthTable[(data >> 3) & 0x1F];
    }
    pulse1.envelopeStart = true;
    pulse1.dutyPosition = 0;
    break;

  // Pulse 2
  case 0x4004:
    pulse2.duty = (data >> 6) & 0x03;
    pulse2.lengthHalt = data & 0x20;
    pulse2.constantVolume = data & 0x10;
    pulse2.envelopePeriod = data & 0x0F;
    break;
  case 0x4005:
    pulse2.sweepEnabled = data & 0x80;
    pulse2.sweepPeriod = (data >> 4) & 0x07;
    pulse2.sweepNegate = data & 0x08;
    pulse2.sweepShift = data & 0x07;
    pulse2.sweepReload = true;
    break;
  case 0x4006:
    pulse2.timerPeriod = (pulse2.timerPeriod & 0xFF00) | data;
    break;
  case 0x4007:
    pulse2.timerPeriod = (pulse2.timerPeriod & 0x00FF) | ((data & 0x07) << 8);
    pulse2.timerValue = pulse2.timerPeriod;
    if (pulse2.enabled) {
      pulse2.lengthCounter = lengthTable[(data >> 3) & 0x1F];
    }
    pulse2.envelopeStart = true;
    pulse2.dutyPosition = 0;
    break;

  // Triangle
  case 0x4008:
    triangle.lengthHalt = data & 0x80;
    triangle.linearCounterReload = data & 0x7F;
    break;
  case 0x400A:
    triangle.timerPeriod = (triangle.timerPeriod & 0xFF00) | data;
    break;
  case 0x400B:
    triangle.timerPeriod =
        (triangle.timerPeriod & 0x00FF) | ((data & 0x07) << 8);
    triangle.timerValue = triangle.timerPeriod;
    if (triangle.enabled) {
      triangle.lengthCounter = lengthTable[(data >> 3) & 0x1F];
    }
    triangle.linearCounterReloadFlag = true;
    break;

  // Noise
  case 0x400C:
    noise.lengthHalt = data & 0x20;
    noise.constantVolume = data & 0x10;
    noise.envelopePeriod = data & 0x0F;
    break;
  case 0x400E:
    noise.mode = data & 0x80;
    noise.noisePeriod = data & 0x0F;
    break;
  case 0x400F:
    if (noise.enabled) {
      noise.lengthCounter = lengthTable[(data >> 3) & 0x1F];
    }
    noise.envelopeStart = true;
    break;

  // DMC
  case 0x4010:
    dmc.irqEnabled = data & 0x80;
    dmc.loop = data & 0x40;
    dmc.rateIndex = data & 0x0F;
    dmc.timerPeriod = dmcRateTable[dmc.rateIndex];
    if (!dmc.irqEnabled) {
      dmc.irqFlag = false;
      dmcIRQ = false;
    }
    break;
  case 0x4011:
    dmc.outputLevel = data & 0x7F;
    break;
  case 0x4012:
    dmc.sampleAddress = 0xC000 | (data << 6);
    break;
  case 0x4013:
    dmc.sampleLength = (data << 4) | 1;
    break;

  // Status
  case 0x4015:
    pulse1.enabled = data & 0x01;
    pulse2.enabled = data & 0x02;
    triangle.enabled = data & 0x04;
    noise.enabled = data & 0x08;
    dmc.enabled = data & 0x10;

    if (!pulse1.enabled)
      pulse1.lengthCounter = 0;
    if (!pulse2.enabled)
      pulse2.lengthCounter = 0;
    if (!triangle.enabled)
      triangle.lengthCounter = 0;
    if (!noise.enabled)
      noise.lengthCounter = 0;

    dmc.irqFlag = false;
    dmcIRQ = false;

    if (dmc.enabled) {
      if (dmc.bytesRemaining == 0) {
        dmc.restart();
      }
    } else {
      dmc.bytesRemaining = 0;
    }
    break;

  // Frame counter
  case 0x4017:
    frameCounterMode = data & 0x80;
    frameIRQInhibit = data & 0x40;

    if (frameIRQInhibit) {
      frameIRQ = false;
    }

    // Reset frame counter
    frameCounter = 0;

    // If mode 1, clock immediately
    if (frameCounterMode) {
      clockQuarterFrame();
      clockHalfFrame();
    }
    break;
  }
}

uint8_t APU2A03::cpuRead(uint16_t addr) {
  uint8_t data = 0;

  if (addr == 0x4015) {
    data |= (pulse1.lengthCounter > 0) ? 0x01 : 0x00;
    data |= (pulse2.lengthCounter > 0) ? 0x02 : 0x00;
    data |= (triangle.lengthCounter > 0) ? 0x04 : 0x00;
    data |= (noise.lengthCounter > 0) ? 0x08 : 0x00;
    data |= (dmc.bytesRemaining > 0) ? 0x10 : 0x00;
    data |= frameIRQ ? 0x40 : 0x00;
    data |= dmc.irqFlag ? 0x80 : 0x00;

    // Reading clears frame IRQ
    frameIRQ = false;
  }

  return data;
}

//========================================
// AUDIO OUTPUT (PROPER NES MIXER)
//========================================
double APU2A03::GetOutputSample() {
  uint8_t p1 = pulse1.output();
  uint8_t p2 = pulse2.output();
  uint8_t tri = triangle.output();
  uint8_t noi = noise.output();
  uint8_t dm = dmc.output();

  // NES APU Mixer
  // Pulse: pulse_out = 95.88 / ((8128 / (pulse1 + pulse2)) + 100)
  double pulseOut = 0.0;
  if (p1 + p2 > 0) {
    pulseOut = 95.88 / ((8128.0 / (double)(p1 + p2)) + 100.0);
  }

  // TND: tnd_out = 159.79 / ((1 / (triangle/8227 + noise/12241 + dmc/22638)) +
  // 100)
  double tndOut = 0.0;
  double tndSum =
      (double)tri / 8227.0 + (double)noi / 12241.0 + (double)dm / 22638.0;
  if (tndSum > 0.0) {
    tndOut = 159.79 / ((1.0 / tndSum) + 100.0);
  }

  // Combined output, normalized to -1.0 to 1.0
  double output = pulseOut + tndOut;

  // The maximum theoretical output is around 1.0, so this normalizes well
  return (output * 2.0) - 1.0;
}
