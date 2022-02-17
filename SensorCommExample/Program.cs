using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO.Ports;
using System.Linq;
using Modbus.Device;


namespace SensorCommExample
{
    public struct ModbusVariable
    { 
        public ushort Address;
        public string Typ;
        public ushort Count;
    }

    public class ModbusUnit
    {
        readonly Dictionary<string, ModbusVariable> _variables = new Dictionary<string, ModbusVariable>
        {
            {"SlaveAddress", new ModbusVariable{Address=0X0800, Typ="UInt16", Count=1 } },
            {"TemperatureTarget", new ModbusVariable{Address=0X0400, Typ="Single", Count=2 } },
            {"TemperatureDet", new ModbusVariable{Address=0X0404, Typ="Single", Count=2 } }
        };

        readonly bool _lsbLowReg = true;

        readonly ModbusSerialMaster _master;
        readonly byte _slaveId;

        public byte SlaveId => _slaveId;

        public ModbusUnit(ModbusSerialMaster master, byte slaveId)
        {
            _master = master;
            _slaveId = slaveId;
        }

        private ushort[] GetPackRegs(ushort[] registers)
        {
            ushort[] reg;

            if (_lsbLowReg)
                reg = registers;
            else
                reg = registers.AsEnumerable().Reverse().ToArray();

            return reg;
        }

        private byte[] GetPackBytes(ushort[] packRegs)
        {
            var byts = new List<byte>();

            foreach (var packReg in packRegs)
                byts.AddRange(BitConverter.GetBytes(packReg));

            return byts.ToArray();
        }

        public object ReadVariableAt(ushort address, string typ, ushort regCount)
        {
            var registers = _master.ReadHoldingRegisters(_slaveId, address, regCount);
            var packRegs = GetPackRegs(registers);
            var packBytes = GetPackBytes(packRegs);

            object variable;
            if(typ == "UInt16")
                variable = BitConverter.ToUInt16(packBytes);
            else if(typ == "Single")
                variable = BitConverter.ToSingle(packBytes);
            else
                throw new ArgumentException("Not supported type!");

            return variable;
        }

        public object ReadVariable(string name)
        {
            var variable = _variables[name];
            return ReadVariableAt(variable.Address, variable.Typ, variable.Count);
        }

        public override string ToString()
        {
            return _master + ", " + _slaveId;
        }

        public static ModbusSerialMaster CreateModbusMaster(string portName)
        {
            var port = new SerialPort(portName)
            {
                BaudRate = 9600, DataBits = 8, Parity = Parity.Even, StopBits = StopBits.One
            };
            port.Open();
            var master = ModbusSerialMaster.CreateRtu(port);
            master.Transport.ReadTimeout = 50;  // Without this line, reading halts after tens of readings
                                                // Connecting "New Laser" Sensor (Model006 Debug)
            master.Transport.WriteTimeout = 50; // Without this, reading halts after displaying for example "searching COM3 1",
                                                // where COM3 is not an "actual" serial port connecting a unit.
                                                // Connecting "New Laser" Sensor (Model006 Debug).

            return master;
        }

    }

    public class Record
    {
        public int Index { set; get; }
        public string Unit { set; get; }
        public string VariableName { set; get; }
        public object VariableValue { set; get; }
    }

    class Program
    {
        static void Main(string[] args)
        {
            byte searchSlaveIdTo;

            if (args.Length == 0)
            {
                searchSlaveIdTo = 16;  // 247 max
            }
            else
            {
                searchSlaveIdTo = (byte)int.Parse(args[0]);
            }

            var comPorts = SerialPort.GetPortNames().OrderBy(n=>n).ToList();
            Console.WriteLine("Found com ports: " + "["+ string.Join(", ", comPorts) + "]");

            var data = new List<Record>();

            foreach(var comPort in comPorts)
            {
                Console.WriteLine("Searching " + comPort);

                using(var master = ModbusUnit.CreateModbusMaster(comPort))
                {
                    var units = new List<ModbusUnit>();
                    for(byte slaveId = 1; slaveId < searchSlaveIdTo + 1; slaveId++)
                    {
                        Console.WriteLine("Searching " + comPort + " " + slaveId);
                        try
                        {
                            var unit = new ModbusUnit(master, slaveId);
                            byte readSlaveId = (byte)(UInt16)unit.ReadVariable("SlaveAddress");
                            Debug.Assert(readSlaveId == slaveId);
                            units.Add(unit);
                        }
                        catch(TimeoutException)
                        {
                        }
                    }

                    Console.WriteLine("Found Units on " + comPort + ": "  + "[" + string.Join(", ", units.Select(u=>u.ToString())) + "]");

                    foreach(var unit in units)
                    {
                        var startTime = DateTime.Now;

                        for(int i = 0; i < 100; i++)
                        {
                            var names = new string[]{ "TemperatureDet", "TemperatureTarget"};
                            foreach(var name in names)
                            {
                                var reading = unit.ReadVariable(name);

                                // Print data
                                Console.WriteLine(i + ", " + name + ", " + reading);

                                // Add data the the database
                                data.Add(new Record { Index = i, Unit = comPort + "-" + unit.SlaveId, VariableName = name, VariableValue = reading });
                            }
                        }

                        var usedTime = DateTime.Now - startTime;
                        Console.WriteLine("Time used: " + usedTime);
                    }
                }
            }

            Console.WriteLine("Done Successfully.");
        }
    }
}
