// This expected table is transcribed from the two design images, independently of production data.
internal static class ExpectedCases
{
    internal sealed record Outcome(int Percent, int Sin = 0, int Heal = 0,
        string Reward = "", int Hit = 0, int Attack = 0, int Camp = 0,
        bool Encounter = false, bool Bridge = false);
    internal sealed record Choice(int Time, Outcome[] Outcomes);
    internal sealed record Definition(string Title, int Weight, Choice[] Choices);
    static Outcome O(int percent = 100, int sin = 0, int heal = 0, string reward = "",
        int hit = 0, int attack = 0, int camp = 0, bool encounter = false, bool bridge = false)
        => new(percent, sin, heal, reward, hit, attack, camp, encounter, bridge);
    static Choice C(int time, params Outcome[] outcomes) => new(time, outcomes);
    internal static readonly Definition[] All =
    {
        new("棺下敲击", 2, new[] {
            C(1, O(70, sin: -15), O(30, encounter: true)), C(0, O()) }),
        new("夜半送葬", 2, new[] {
            C(1, O(sin: -15)), C(0, O(sin: 20, reward: "绷带")) }),
        new("被封死的门", 2, new[] {
            C(1, O(sin: -12, hit: 15)), C(1, O(60, sin: -15), O(40, encounter: true)) }),
        new("被弃的药箱", 3, new[] {
            C(1, O(heal: 25)), C(0, O(65, reward: "绷带"), O(35, encounter: true)) }),
        new("霉烂粮车", 2, new[] {
            C(1, O(sin: -15, heal: 10)), C(0, O(sin: 20, reward: "绷带")) }),
        new("荒废告解室", 2, new[] {
            C(1, O(sin: -12, hit: 15)), C(0, O(60, reward: "净化剂"), O(40, encounter: true)) }),
        new("无字墓碑", 2, new[] {
            C(1, O(sin: -15, attack: 15)), C(0, O(sin: 15, reward: "绷带")) }),
        new("断桥上的绳索", 1, new[] {
            C(2, O(bridge: true)), C(0, O(60, reward: "绷带"), O(40, encounter: true)) }),
        new("自罚游行", 1, new[] {
            C(1, O(80, sin: -15), O(20, sin: -15, encounter: true)),
            C(0, O(60, reward: "绷带"), O(40, encounter: true)) }),
        new("最后一封家书", 2, new[] {
            C(1, O(sin: -15, camp: 20)), C(0, O(sin: 15, heal: 15)) })
    };
}
