using System.Collections.Generic;
using UnityEngine;

// Dialogue stays in the shared, paginated presentation. No new combat effects are
// attached to the ending: the player's decision is carried into the village coda.
public static class SlothChapterDialogue
{
    public static DialogueData Hook()
    {
        var d = new Builder("sloth_hook");
        d.Line("door", "旁白", "门外响起一阵急促的敲门声。来人显然没打算等到屋里的人方便。");
        d.Line("help", "男人", "哈罗德大人！我妻子她……求您想想办法！");
        d.Line("doctor", "哈罗德", "去找西奥多。他懂医术，他会——");
        d.Line("closed", "男人", "去过了。门关着。我一直在门口喊，告诉他，人已经病成什么样了。");
        d.Line("tomorrow", "男人", "他说，明天再来。");
        d.Line("care", "哈罗德", "先回去陪着她。我让人送水和药过去，再请莫德替她看看。不能让她就这么等着。");
        d.Line("depart", "旁白", "男人点了两次头，转身跑了。哈罗德叫住门边的村民，把方才的话一件件交代下去。");
        d.Choice("ask", "哈罗德", "西奥多从前救过不少人。可如今……他只肯跪着祷告。谁去找，都是明天。",
            "教堂里怎么回事？", "explain", "我先做些准备。", "later");
        d.Line("explain", "哈罗德", "他领着几个人，去了村外的旧祭所。埃德蒙神父劝过许多回，没劝动。您若想弄清缘由，来问我，我把神父也请来。", "close");
        d.Line("later", "哈罗德", "明白。您也才刚回来。病人这边先由我们照料；您准备好了，再来找我。", "close");
        d.Line("close", "旁白", "屋外有人提起水桶，有人匆匆往药师家去。至少这一回，哈罗德没有让他们等到明天。", "");
        return d.Build();
    }

    public static DialogueData Investigation()
    {
        var d = new Builder("sloth_investigation");
        d.Line("welcome", "哈罗德", "您是为教堂的事来的？坐吧。那边这几天，倒是越发热闹了。");
        d.Line("past", "哈罗德", "西奥多从前四处传道，也替人看病。瘟疫那年留下来，我还以为是我们的福气。如今他领着六个人，整日跪在村外的旧祭所，拿鞭子抽自己的背。");
        d.Line("water", "哈罗德", "起初他说，这是替大家赎罪。我还给他们送过水，叫人别去打扰。后来送水的也跪下了。");
        d.Line("chores", "哈罗德", "田里没人，井边没人，病人要喝口汤，得先看照顾他的人是不是还在祷告。汤玛斯家的小子，扛起麦袋就能跑。如今跪在那儿，谁叫都不应。");
        d.Line("agnes", "哈罗德", "还有艾格尼丝。她一直替教堂洒扫，病得原本不重。西奥多让她安心等候，她便当真等着。前天没了，床边的粥都没动。");
        d.Line("question", "猎魔人", "西奥多一直是这样的人？");
        d.Line("failures", "哈罗德", "从前不是。那双手治好过人，我亲眼见过。后来没能救回来的人多了，他便越来越少肯碰药箱。有一回，他问我：若再有人死在他手里，算谁的罪。");
        d.Line("avoid", "哈罗德", "如今他觉得，只要自己跪得够久、流的血够多，剩下的事就该有人替他办了。可门外求救的人，总得找着个肯开门的。");
        d.Line("priest_arrives", "旁白", "埃德蒙神父在门口停下，先把一包药交给等候的村民。听见西奥多的名字，他把刚迈出去的脚收了回来。");
        d.Line("edmond", "埃德蒙", "哈罗德捎过话。您来得正好，我也实在想不出还能找谁。药和热水已经送去了，病人有人照料。西奥多那边，却仍旧不肯起来。");
        d.Line("dilemma", "埃德蒙", "我若去看病人，他说我不肯等候；我若去劝他，他又说我不懂受苦。无论我做什么，总是他跪得更有道理。");
        d.Choice("questions", "埃德蒙", "您想知道他到底在同我争什么？",
            "他说流血就能替大家受罚。", "fruit",
            "他说做工救不了灵魂，只须等候。", "faith",
            "我该怎么劝他？", "burdens");
        d.Line("fruit", "埃德蒙", "施洗的约翰也说过：『你们要结出果子来，与悔改的心相称。』我问他，跪了一天，可有人得了饭吃？他就把背上的伤给我看。");
        d.Line("bowls", "埃德蒙", "那些伤当然是真的，可病人的碗也还是空的。", "questions");
        d.Line("faith", "埃德蒙", "『身体没有灵魂是死的，信心没有行为也是死的。』他从前也读过，大概如今不大愿意翻到那一页。");
        d.Line("wood", "埃德蒙", "我不求他治好所有人。抬桶水，添把柴，总做得到吧？可他现在连起身都觉得是在冒犯神。", "questions");
        d.Line("burdens", "埃德蒙", "『你们各人的重担要互相担当。』满村子都是担着水、抱着病孩子的人。他倒好，把所有重担都揽成了自己背上的几道口子。");
        d.Line("garden", "埃德蒙", "神把人安置在园子里，让他修理看守。若光跪着就行，那园子早该自己长好了。");
        d.Line("advice", "埃德蒙", "别只同他争谁有道理。您说您的，他就说您不信。把他读过的经文还给他，再问问艾格尼丝的事。至少这一次，他得先听完。");
        d.Line("request", "埃德蒙", "若他真变成了城外那样的东西……您该怎么做，就怎么做。我只盼您还能把他救回来。");
        d.Line("route", "哈罗德", "他们在村外的旧祭所。沿着通往懒惰盘踞处的路走，便能找到。准备妥当再动身，村里的病人我们会继续照料。", "");
        return d.Build();
    }

    public static DialogueData Confrontation(bool investigated)
    {
        var d = new Builder("sloth_confrontation");
        if (!investigated)
        {
            d.Line("arrival", "旁白", "旧祭所旁的路上，一个男人正提着空药罐往回走。他说，妻子病着，村里正在替他想办法。他来找过西奥多，对方只叫他明天再来。");
            d.Line("context", "男人", "他从前会看病。后来带着这些人跪下，药箱也不碰了。艾格尼丝就是这么等的，床边的粥一口没动，人已经没了。");
            d.Line("memory", "旁白", "你望向祭所。信心没有行为也是死的——那句话并不陌生。可眼前的人，似乎早已忘了它。");
        }
        d.Line("steps", "旁白", "六个人跪在旧祭所的石阶上，白衣背后洇着血，手里各攥一条鞭子。那几级台阶被磨得发亮，至少有一样东西从他们的虔诚里得了好处。");
        d.Line("warning", "旁白", "西奥多跪在最前头。灰白的尘屑从他袖口落下，膝下的影子仿佛陷进了石缝。你的影子落到他身侧，他连头也没抬。");
        d.Line("leave", "西奥多", "若是来叫我们起来的，您可以回去了。");
        d.Choice("debate1", "西奥多", "瘟疫是神的怒气。总得有人替大家受罚。我多挨一鞭，便有人少受一分苦。您有您的差事，我有我的。",
            "经上说，要结出果子来，与悔改的心相称。你流了这些血，究竟救了谁？", "fruit_reply",
            "别抽了。神根本不吃你这一套。", "wrong1_god",
            "田里还有一堆活，先起来做完再说。", "wrong1_work",
            "听起来不错。给我留个位置。", "wrong1_kneel");
        d.Line("fruit_reply", "旁白", "鞭子停在半空。西奥多慢慢把它放回腿上。");
        d.Line("fruit_defence", "西奥多", "我们受的苦，自然就是果子。神看得见。", "debate2");
        d.Line("wrong1_god", "西奥多", "您同神商量过了？", "debate1");
        d.Line("wrong1_work", "西奥多", "您只看得见田里的麦子。我在救他们的灵魂。", "debate1");
        d.Line("wrong1_kneel", "内心·懒惰", "跪下吧。什么也不必决定，什么也不必负责。膝盖疼，总好过操心。");
        d.Line("rise_again", "旁白", "你在他身侧跪了一会儿。石阶比想象中硬，病人的呻吟却比想象中近。闭上眼睛，原来也没能把声音一并关掉。你重新站了起来。", "debate1");
        d.Choice("debate2", "西奥多", "您说救人。可人的手做再多工，也救不了灵魂。我只须静心等候主。",
            "信心没有行为也是死的。这句经文，你也读过吧？", "faith_reply",
            "等候主？他告诉你什么时候来了吗？", "wrong2_wait",
            "坐着等别人收拾烂摊子，我也会。", "wrong2_wounds");
        d.Line("faith_reply", "旁白", "他的手指一点点收紧，鞭柄上的旧皮在掌心里吱了一声。");
        d.Line("faith_defence", "西奥多", "我心里信。这便够了。", "debate3");
        d.Line("wrong2_wait", "西奥多", "主来时像贼一样。难道还须先向您通报？", "debate2");
        d.Line("wrong2_wounds", "西奥多", "您倒肯同我换一换？", "wrong2_pause");
        d.Line("wrong2_pause", "旁白", "他把血淋淋的后背转了转。那些伤确实不轻。你停了一下，重新开口。", "debate2");
        d.Choice("debate3", "西奥多", "我为他们受苦。每一鞭，都有人得救。我没有什么对不起他们的。",
            "艾格尼丝的粥放在床边，一口没动。你让她等。她等到你救她了吗？", "agnes_reply",
            "你谁也救不了，连自己都救不了。", "wrong3_sword");
        d.Line("agnes_reply", "旁白", "西奥多的嘴唇动了动。他低下头，第一次没有立刻接上下一句经文。");
        d.Line("agnes_defence", "西奥多", "我……是叫她安心等候。我以为……", "debate4");
        d.Line("wrong3_sword", "西奥多", "您凭什么断定？就凭腰上那把剑？", "debate3");
        d.Choice("debate4", "旁白", "西奥多张开嘴，又合上。他看了看手里的鞭子，像头一次不知该拿它怎么办。身后的六个人也停了下来。没有鞭声之后，你才发觉这里一直有人在粗重地喘气。",
            "先把鞭子放下。你已经没有话能替它辩护了。", "collapse");
        d.Line("collapse", "旁白", "他的肩背猛地绷紧。某种更深的倦意从眼底漫上来，连方才的慌乱也一并吞了下去。早已扎进石缝的阴影，顺着他的身体往上攀。");
        d.Line("rest", "西奥多", "没有话……那便不说了。让我歇一歇。");
        d.Line("monster", "旁白", "皮鞭垂落。你听见布料拉扯、骨头错位的声音。灰白的尘雾从那副沉重的躯体间涌出，将人的面孔一寸寸遮住。身后的信徒终于往后退去。");
        d.Line("fight", "旁白", "尘雾深处，有东西缓慢地抬起了手。你握紧武器。", "");
        return d.Build();
    }

    public static DialogueData AfterBattle(SlothFate fate)
    {
        var d = new Builder("sloth_after_battle");
        if (fate != SlothFate.Undecided)
        {
            d.Line("resume", "旁白", fate == SlothFate.Spared
                ? "你已经收回武器。信徒扶住西奥多，等着与你一道回村。欠下的事，还要有人一件件做完。"
                : "西奥多倒在石阶旁。信徒们放下鞭子，拾起地上的药箱。村里还有人在等。", "");
            return d.Build();
        }
        d.Line("dust", "旁白", "尘雾散了。那副沉重的躯壳终于塌下，西奥多伏在碎屑间，重新喘起气来。");
        d.Line("hands", "旁白", "他看着自己的手，又回头看那些信徒。几个人仍坐在地上，像还不明白该把目光放在哪里。");
        d.Line("waiting", "西奥多", "我让他们等……我一直叫他们等。");
        d.Line("porridge", "西奥多", "艾格尼丝还问过我，她是不是该吃点东西。我告诉她，先祷告……");
        d.Line("silence", "旁白", "后半句话没能说下去。他瘫在你脚边，连膝盖都还撑不直。周围的人等着你决定，他接下来还有没有站起来的必要。");
        d.Line("sloth_voice", "内心·懒惰", "你已经替他拼过一回命了。还要替他安排往后的日子？他得吃饭，得有人看着。若再犯一次，大家还得找你。");
        d.Line("sloth_offer", "内心·懒惰", "一击就够了。他不再受苦，你也省事。");
        d.Line("diligence", "内心·勤勉", "他刚才说出了艾格尼丝的名字。他记得自己做过什么。那双手还会看病，也拿得动锄头。让他起来，把欠下的事一件件做完。");
        d.Line("cost", "内心·勤勉", "是会麻烦些。得有人盯，得有人教。可这村子如今，已经少不起多少双手了。");
        d.Choice("judgement", "旁白", "西奥多抬起头。你的武器还握在手里。",
            "到此为止吧。", "fate_killed",
            "起来。跟我回村，你还有事要做。", "fate_spared");
        d.Line("fate_killed", "旁白", "武器落下。西奥多的身体往旁边一歪，终于不再喘气。石阶上又添了一道血痕。", "killed_witnesses");
        d.Line("killed_witnesses", "旁白", "信徒们盯着那道血痕。过了一会儿，有人放下鞭子，去拾西奥多搁在墙角的药箱。其他人也一个接一个站了起来。", "return");
        d.Line("fate_spared", "旁白", "你收回武器，向他伸出手。他迟疑了片刻，才把那只沾满灰土的手放进你掌心。很沉。你还是把他拉了起来。", "spared_terms");
        d.Line("spared_terms", "猎魔人", "回去见哈罗德和神父。你欠下的事，由他们看着你做。", "spared_reply");
        d.Line("spared_reply", "西奥多", "……药箱还在墙角。里面有些药，能用。", "return");
        d.Line("return", "旁白", "旧祭所终于安静下来。回村的路就在身后，还有人等着你带回消息。", "");
        return d.Build();
    }

    public static DialogueData ReturnToVillage(SlothFate fate)
    {
        bool spared = fate == SlothFate.Spared;
        var d = new Builder(spared ? "sloth_return_spared" : "sloth_return_killed");
        d.Line("home", "旁白", "你回到埃尔南多时，哈罗德已经等在门口。埃德蒙神父站在他身旁，先看了看你，又看了看跟在后面的人。");
        d.Line("report", "哈罗德", "祭所那边……");
        d.Line("resolved", "猎魔人", "结束了。");
        if (spared)
        {
            d.Line("theodore", "旁白", "西奥多被人扶到门前。他没有再跪下，只把药箱放在脚边，低着头说起艾格尼丝的名字。");
            d.Line("account", "埃德蒙", "把你知道的都说清楚。谁还病着，药该怎么用，谁被你叫去等了。说完，我们一户一户去。");
            d.Line("supervision", "哈罗德", "往后你不能再领着他们。看病、送药、田里的活，都由神父和村里的人看着你做。欠下多少，便记多少。");
            d.Line("no_pardon", "旁白", "没有人说原谅。西奥多点了点头，蹲下去，重新打开那只落了灰的药箱。");
            d.Line("husband", "男人", "西奥多……我妻子她，药师暂时替她稳住了。您能不能再去看看？");
            d.Line("answer", "西奥多", "能。带上热水。我跟你走。");
            d.Line("today", "旁白", "这一次，他没有叫人等到明天。");
            d.Line("afterword", "旁白", "后来，送水的人重新挑起了水桶，田里也有了动静。西奥多在神父的监督下为病人配药。那位男人家的门开着，灶上又煮起粥来。");
        }
        else
        {
            d.Line("death", "猎魔人", "西奥多死了。");
            d.Line("priest", "旁白", "埃德蒙闭了闭眼。他接过信徒抱着的药箱，掀开盖子，把几只药瓶一一扶正。");
            d.Line("work", "埃德蒙", "会挑水的，去井边。会生火的，去病人家里。剩下的人跟着我，把还等着照料的人记下来。");
            d.Line("missing", "哈罗德", "莫德已经在照看那位病人。我再找两个肯帮忙的人过去。西奥多留下的那些活，总得有人接着做。");
            d.Line("husband", "男人", "神父……我妻子还在等。");
            d.Line("answer", "埃德蒙", "我知道。走吧。");
            d.Line("today", "旁白", "门外的人还在等。神父提起药箱，走了出去。");
            d.Line("afterword", "旁白", "后来，送水的人重新挑起了水桶，田里也有了动静。那位男人的妻子有人照料，灶上又煮起粥来。村里少了一双会看病的手，留下的人得多做一些。");
        }
        d.Line("thanks", "哈罗德", "这回，我们不会只等着您来收拾了。该做的事，我们自己动手。谢谢您，猎魔人。");
        d.Line("coda", "旁白", "你走过村口，听见屋里有人添柴，井边有人提水。那些声音都很寻常。这个村子已经许久没有这样寻常过了。", "");
        return d.Build();
    }

    sealed class Builder
    {
        readonly DialogueData data;
        readonly List<DialogueNode> nodes = new List<DialogueNode>();
        readonly HashSet<string> automaticNext = new HashSet<string>();
        public Builder(string id)
        {
            data = ScriptableObject.CreateInstance<DialogueData>();
            data.name = data.dialogueId = id;
            data.hideFlags = HideFlags.DontSave;
            data.completionEffect = StoryDialogueEffect.None;
        }
        public void Line(string id, string speaker, string text, string next = null)
        {
            if (nodes.Count > 0 && automaticNext.Remove(nodes[nodes.Count - 1].id))
                nodes[nodes.Count - 1].nextNodeID = id;
            var node = new DialogueNode {
                id = id, speaker = speaker, content = text, nextNodeID = next,
                isProtagonist = speaker == "猎魔人",
                lineKind = speaker == "旁白" ? DialogueLineKind.Narration : DialogueLineKind.Dialogue,
                choices = new List<DialogueChoice>()
            };
            nodes.Add(node);
            if (next == null) automaticNext.Add(id);
        }
        public void Choice(string id, string speaker, string text, params string[] choices)
        {
            Line(id, speaker, text, "");
            var node = nodes[nodes.Count - 1];
            for (int i = 0; i < choices.Length; i += 2)
                node.choices.Add(new DialogueChoice { choiceText = choices[i], targetNodeID = choices[i + 1] });
        }
        public DialogueData Build()
        {
            data.startNode = nodes[0];
            data.nodes = nodes.GetRange(1, nodes.Count - 1);
            return data;
        }
    }
}
