namespace CgsLedConsole;

public sealed class CommandNode {
    public delegate void RunFunc(ReadOnlySpan<string> args);

    public string name { get; }
    public IReadOnlyList<CommandNode>? children { get; }
    public RunFunc? func { get; }

    public CommandNode(string name, IReadOnlyList<CommandNode>? children) {
        this.name = name;
        this.children = children;
    }

    public CommandNode(string name, RunFunc? func) {
        this.name = name;
        this.func = func;
    }

    public void Run(ReadOnlySpan<string> args) {
        if(children is null) {
            func?.Invoke(args);
            return;
        }
        foreach(CommandNode child in children) {
            if(child.name != args[0])
                continue;
            child.Run(args[1..]);
            return;
        }
        Console.WriteLine("Unknown command");
    }
}
