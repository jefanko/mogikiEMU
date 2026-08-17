# ROM-based integration fixtures

These fixtures come from the public `christopherpow/nes-test-roms` collection:

<https://github.com/christopherpow/nes-test-roms>

They are test programs, not commercial game ROMs. SHA-256 values are recorded
below so the fixtures can be replaced or audited without silently changing the
test inputs.

| Fixture | Upstream test | SHA-256 |
| --- | --- | --- |
| `nestest.nes` | `other/nestest.nes` | `F67D55FD6B3CF0BAD1CC85F1DF0D739C65B53E79CECB7FEA8F77EC0EADAB0004` |
| `blargg_cpu_official.nes` | `blargg_nes_cpu_test5/official.nes` | `5B412B3940ABE3F9ED562B86B93CF660688BEE95EBA927C4DACD53C9DA89FE9A` |
| `instr_test_basics.nes` | `instr_test-v5/rom_singles/01-basics.nes` | `4DD1CDD406BC3F747972E7DA314CE8CA89321EB7A836C1CED569EE54AE44A384` |
| `instr_test_implied.nes` | `instr_test-v5/rom_singles/02-implied.nes` | `1C4D4FA130CF6FEEBC072543A5CD3627AE71063B56B08642BF43E9A6C6F44996` |
| `instr_test_branches.nes` | `instr_test-v5/rom_singles/10-branches.nes` | `63AB768E88931DB6F7DFCFAFE43D5E29EBC3DCB80DA8FC7FCDA8C930F34AEF54` |
| `instr_test_official.nes` | `instr_test-v5/official_only.nes` | `589B8835DEB5CBC69618DAC193A3DBD675540F7F2794E2D2A92E97BEB8ABC3CB` |
| `ppu_vbl_nmi.nes` | `ppu_vbl_nmi/ppu_vbl_nmi.nes` | `8DBAB1BE785585C399CF055EF02147B788AB75FD80E81CF9568A2FEAFC03FB7D` |
| `ppu_vbl_basics.nes` | `ppu_vbl_nmi/rom_singles/01-vbl_basics.nes` | `06AEA5AF4EDAB4E3141C939CD5AC9936F8758203B25DCAF84AE1A09DB49E024A` |
| `ppu_vbl_set_time.nes` | `ppu_vbl_nmi/rom_singles/02-vbl_set_time.nes` | `DD98856130078844E3AA4BD95A9BE8AB501EA84C089F1D8AD49A1B20AF4B3A80` |
| `ppu_nmi_control.nes` | `ppu_vbl_nmi/rom_singles/04-nmi_control.nes` | `84722C75B896C47C8642F83220230FE14F0A31E55E26ECB83C400E6A26D91B32` |
| `read_joy3_test_buttons.nes` | `read_joy3/test_buttons.nes` | `15F53317FD2ADF8454256FDAFB4EA6C5FB27940166ADCDADD17E9E6FB94FDFAC` |

The interactive controller fixture is included for manual validation. The
automated controller tests live in `BusTests.cs` because this ROM waits for
physical button presses.
