using System.IO.Ports;

namespace CgsLedController;

public class SerialPortLedWriter : LedWriter {
    public override bool isOpen => _port.IsOpen;

    private readonly SerialPort _port;
    private bool _canContinue = true;

    public SerialPortLedWriter(IReadOnlyList<int> ledCounts, SerialPort port) : base(ledCounts) => _port = port;

    public override void Open() {
        _port.DtrEnable = true;
        _port.Open();
        // wait for arduino to reset
        bool canContinue = false;
        while(!canContinue) {
            while(_port.BytesToRead > 0) {
                if(_port.ReadByte() == 1)
                    canContinue = true;
            }
        }
    }

    public override void Close() {
        while(_port.BytesToWrite > 0) { }
        _port.Close();
    }

    protected override void Ping() {
        Write((byte)DataType.Ping);
        while(_port.BytesToWrite > 0) { }
        while(!_canContinue) {
            while(_port.BytesToRead > 0) {
                if(_port.ReadByte() == 0)
                    _canContinue = true;
            }
        }
        _canContinue = false;
    }

    protected override void WriteInternal(byte[] bytes, int count) => _port.Write(bytes, 0, count);
}
