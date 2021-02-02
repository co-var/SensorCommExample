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
        Dictionary<string, ModbusVariable> variables = new Dictionary<string, ModbusVariable>
        {
            {"SlaveAddress", new ModbusVariable{Address=0X0800, Typ="UInt16", Count=1 } },
            {"TemperatureDet", new ModbusVariable{Address=0X0404, Typ="Single", Count=2 } }
        };

        bool _lsbLowReg = true;

        ModbusSerialMaster _master;
        byte _slaveId;

        public ModbusUnit(ModbusSerialMaster master, byte slave_id)
        {
            _master = master;
            _slaveId = slave_id;
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

        private byte[] GetPackBytes(ushort[] pack_regs)
        {
            var byts = new List<byte>();

            foreach (var pack_reg in pack_regs)
                byts.AddRange(BitConverter.GetBytes(pack_reg));

            return byts.ToArray();
        }

        public object ReadVariableAt(ushort address, string typ, ushort reg_count)
        {
            var registers = _master.ReadHoldingRegisters(_slaveId, address, reg_count);
            var pack_regs = GetPackRegs(registers);
            var pack_bytes = GetPackBytes(pack_regs);

            object variable;
            if(typ == "UInt16")
                variable = BitConverter.ToUInt16(pack_bytes);
            else if(typ == "Single")
                variable = BitConverter.ToSingle(pack_bytes);
            else
                throw new ArgumentException("Not supported fmt!");

            return variable;
        }

        public object ReadVariable(string name)
        {
            var address = variables[name].Address;
            var typ = variables[name].Typ;
            var count = variables[name].Count;
            return ReadVariableAt(address, typ, count);
        }

        public override string ToString()
        {
            return _master + ", " + _slaveId;
        }

        public static ModbusSerialMaster CreateModbusMaster(string port_name)
        {
            var port = new SerialPort(port_name);
            port.BaudRate = 9600;
            port.DataBits = 8;
            port.Parity = Parity.Even;
            port.StopBits = StopBits.One;
            port.Open();
            var master = ModbusSerialMaster.CreateRtu(port);
            // master.Transport.CheckFrame = True
            master.Transport.ReadTimeout = 50;  // Without this line, reading halts after tens of readings. Using NModbus4.dll.
            // Connecting "New Laser" Sensor (Model006 Debug)
            // master.Transport.WriteTimeout = 50
            return master;
        }

    }

    class Program
    {
        static void Main(string[] args)
        {
            
            var com_ports = SerialPort.GetPortNames().OrderBy(n=>n);
            Console.WriteLine("Found com ports: " + "["+ string.Join(", ", com_ports) + "]");

            var readings_of_units_of_ports = new List<List<List<object>>>();
            foreach(var com_port in com_ports)
            {
                Console.WriteLine("Searching " + com_port);
                var readings_of_units = new List<List<object>>();

                using(var master = ModbusUnit.CreateModbusMaster(com_port))
                {
                    var units = new List<ModbusUnit>();
                    byte search_slave_id_to = 16;  // 247 max
                    for(byte slave_id = 1; slave_id < search_slave_id_to + 1; slave_id++)
                    {
                        Console.WriteLine("Searching " + com_port + " " + slave_id);
                        try
                        {
                            var unit = new ModbusUnit(master, slave_id);
                            Debug.Assert((byte)(UInt16)unit.ReadVariable("SlaveAddress") == slave_id);
                            units.Add(unit);
                        }
                        catch(TimeoutException)
                        {
                        }
                    }

                    Console.WriteLine("Found Units on " + com_port + ": "  + "[" + string.Join(", ", units.Select(u=>u.ToString())) + "]");

                    foreach(var unit in units)
                    {
                        var readings_of_unit_of_port = new List<object>();
                        var start_time = DateTime.Now;

                        for(int i = 0; i < 100; i++)
                        {
                            var reading = unit.ReadVariable("TemperatureDet");
                            readings_of_unit_of_port.Add(reading);
                            Console.WriteLine(i+ ": " + reading);
                        }

                        var used_time = DateTime.Now - start_time;
                        Console.WriteLine("Time used: " + used_time);
                        readings_of_units.Add(readings_of_unit_of_port);
                    }
                }

                readings_of_units_of_ports.Add(readings_of_units);
            }

            var readings_of_all_units = new List<List<object>>();
            foreach(var readings_of_units_of_port in readings_of_units_of_ports)
            {
                foreach(var readings_of_unit_of_port in readings_of_units_of_port)
                    readings_of_all_units.Add(readings_of_unit_of_port);
            }

            Console.WriteLine("Done Successfully.");
        }
    }
}
