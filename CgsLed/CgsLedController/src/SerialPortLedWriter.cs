using System.IO.Ports;

namespace CgsLedController;

public class SerialPortLedWriter : LedWriter {
    public override bool isOpen => _port.IsOpen;

    private readonly SerialPort _port;
    private bool _canContinue = true;

    // SerialPort API is stupid and doesn't let me write a span....
    private readonly byte[] _bytes = new byte[1024];

    public SerialPortLedWriter(SerialPort port) => _port = port;

    public void Open() {
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

    public void Close() {
        while(_port.BytesToWrite > 0) { }
        _port.Close();
    }

    public override void Ping(LedBuffer buffer) {
        buffer.Write((byte)DataType.Ping);
        while(_port.BytesToWrite > 0) { }
        while(!_canContinue) {
            while(_port.BytesToRead > 0) {
                if(_port.ReadByte() == 0)
                    _canContinue = true;
            }
        }
        _canContinue = false;
    }

    public override void Write(LedBuffer.LedData[] data, int count, float brightness) {
        for(int i = 0; i < count; i++)
            _bytes[i] = data[i].Get(brightness);
        _port.Write(_bytes, 0, count);
    }
}
