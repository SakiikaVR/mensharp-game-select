using System;
using UnityEngine;

// Pure state-transition checks run on the C# proxy, followed by separate ClientSim Udon checks.
public static class DaifugoRulesTests
{
    private static DaifugoGame g;
    private static int assertions;
    private static void Check(bool v,string reason){assertions++;if(!v)throw new Exception("Daifugo: "+reason);}
    private static void Reset(int bits=2047)
    {
        g.disqualified=new bool[4];g.penaltyCount=0;g.rules=bits;g.phase=1;g.seats=new[]{1,2,3,0};g.names=new[]{"A","B","C",""};g.owners=new int[53];
        for(int c=0;c<53;c++)g.owners[c]=-1;
        g.owners[51]=0;g.owners[50]=1;g.owners[49]=2;
        g.places=new int[4];g.previousPlaces=new int[4];g.passed=new bool[4];g.turn=0;g.lastSeat=-1;g.pileCount=0;g.pileRank=-1;g.pileSuit=0;g.lockSuit=0;g.pileStraight=false;g.pileJoker=false;g.revolution=false;g.jackBack=false;g.finished=0;g.pending=0;g.pendingSeat=-1;g.giveRemaining=g.discardRemaining=g.bombRemaining=g.skipCount=0;g.clearAfter=false;g.effectMask=0;
    }
    private static void Give(int seat,params int[] cards){foreach(int c in cards)g.owners[c]=seat;}
    private static bool Play(int seat,params int[] cards)
    {int lo=0,hi=0;foreach(int c in cards){if(c<27)lo|=1<<c;else hi|=1<<(c-27);}return g.TryPlay(seat,lo,hi);}
    public static string Run()
    {
        var o=new GameObject("DaifugoRuleTest");o.SetActive(false);g=o.AddComponent<DaifugoGame>();assertions=0;
        try
        {
            Reset(0);Give(0,0);Give(1,1);Check(!Play(1,1),"out of turn");Check(!Play(0,1),"foreign hand");Check(Play(0,0),"single lead");Check(Play(1,1),"single stronger");Check(g.TryPass(2),"pass");Check(g.TryPass(0),"pass clears");Check(g.pileCount==0&&g.turn==1,"last player leads");
            Reset(0);Give(0,0,13);Check(Play(0,0,13),"pair");Give(1,1);Check(!Play(1,1),"same count required");
            Reset(1<<3);Give(0,5);Check(Play(0,5)&&g.pileCount==0&&g.turn==0,"eight cut");
            Reset(0);Give(0,5);Check(Play(0,5)&&g.pileCount==1,"eight disabled");
            Reset(1<<2);Give(0,8);Give(1,7);Check(Play(0,8)&&g.jackBack,"J back");Check(Play(1,7),"J reverse order");g.TryPass(2);g.TryPass(0);Check(!g.jackBack,"J resets at clear");
            Reset(1<<5);Give(0,1,14,27,40);Check(Play(0,1,14,27,40)&&g.revolution,"revolution");
            Reset(1<<5);Give(0,0,13,26,39,52);Check(Play(0,0,13,26,39,52)&&g.revolution,"five card revolution");
            Reset(1<<6);Give(0,0,1,2);Check(Play(0,0,1,2)&&g.pileStraight,"stairs");
            Reset(0);Give(0,0,1,2);Check(!Play(0,0,1,2),"stairs disabled");
            Reset((1<<6)|(1<<7));Give(0,0,1,2,3);Check(Play(0,0,1,2,3)&&g.revolution,"stairs revolution");
            Reset(1<<6);Give(0,0,2,52);Check(Play(0,0,2,52)&&g.pileStraight,"joker fills stair gap");
            Reset(1<<6);Give(0,0,3,52);Check(!Play(0,0,3,52),"two stair gaps illegal");
            Reset(1<<8);Give(0,52);Give(1,0);Check(Play(0,52),"joker");Check(Play(1,0)&&g.pileCount==0&&g.turn==1,"spade three return");
            Reset(0);Give(0,52);Give(1,0);Play(0,52);Check(!Play(1,0),"spade return disabled");
            Reset(1<<4);Give(0,0);Give(1,1);Give(2,15,2);Play(0,0);Play(1,1);Check(g.lockSuit==1,"suit bind");Check(!Play(2,15),"other suit rejected");Check(Play(2,2),"bound suit accepted");
            Reset(1);Give(0,7,1);Check(Play(0,7)&&g.pending==1&&g.turn==0,"ten pending");Check(!g.TryPass(0),"cannot pass effect");Check(Play(0,1)&&g.owners[1]==-1&&g.pending==0,"ten discard");
            Reset(1<<9);Give(0,4,1);Check(Play(0,4)&&g.pending==2,"seven pending");Check(Play(0,1)&&g.owners[1]==1,"seven transfer");
            Reset(1<<1);Give(0,9,0);Give(1,13);Give(2,26);Check(Play(0,9)&&g.pending==3,"Q pending");Check(!g.TryBomb(1,0),"other player cannot bomb");Check(g.TryBomb(0,0)&&g.owners[0]==-1&&g.owners[13]==-1&&g.owners[26]==-1,"bomb all hands");
            Reset(1<<10);Give(0,2);Check(Play(0,2)&&g.turn==2,"five skip");
            Reset(0);Give(0,2);Check(Play(0,2)&&g.turn==1,"five disabled");
            Reset(1<<10);g.seats[2]=0;Give(0,2);Check(Play(0,2)&&g.turn==0&&g.pileCount==1,"five wraps without clearing");
            Reset((1<<6)|(1<<2));Give(0,5,6,7,8);Give(1,17,18,19,20);Check(Play(0,5,6,7,8)&&g.jackBack,"stair J back");Check(Play(1,17,18,19,20),"stair strength stays consistent after J flip");
            Reset();g.owners[51]=-1;Give(0,5);Check(Play(0,5)&&g.places[0]==1&&g.turn==1,"finish on eight");
            Reset();g.owners[51]=-1;g.owners[49]=-1;Give(0,7,0);Give(2,26);Play(0,7);Play(0,0);Check(g.places[0]==1,"finish on discard");
            foreach(int c in new[]{12,0,5,52})
            {
                Reset(4095);g.owners[51]=-1;Give(0,c);g.revolution=c==0;
                Check(Play(0,c),"forbidden move resolves");Check(g.disqualified[0]&&g.places[0]==3,"forbidden finish is last");
            }
            Reset(4095);g.owners[51]=-1;Give(0,0);Check(Play(0,0)&&!g.disqualified[0],"normal three allowed");
            Reset(4095);g.owners[51]=-1;g.revolution=true;Give(0,12);Check(Play(0,12)&&!g.disqualified[0],"revolution two allowed");
            Reset(2047);g.owners[51]=-1;Give(0,12);Check(Play(0,12)&&!g.disqualified[0],"forbidden option off");
            // Complete shuffled rounds exercise pending effects, passes, and ranking without an external client.
            for(int run=0;run<20;run++)
            {
                Reset();g.phase=0;g.Deal();Check(g.HandCount(0)+g.HandCount(1)+g.HandCount(2)+g.HandCount(3)==53,"all 53 dealt");int steps=0;
                while(g.phase!=2&&steps++<3000)
                {
                    int s=g.turn;
                    if(g.pending==3){Check(g.TryBomb(s,steps%13),"random bomb");continue;}
                    if(g.pending!=0)
                    {
                        int lo=0,hi=0,n=0;for(int c=0;c<53&&n<g.pendingCount;c++)if(g.owners[c]==s){if(c<27)lo|=1<<c;else hi|=1<<(c-27);n++;}
                        Check(g.TryPlay(s,lo,hi),"random effect");continue;
                    }
                    bool played=false;for(int c=0;c<53;c++)if(g.owners[c]==s&&Play(s,c)){played=true;break;}
                    if(!played)Check(g.TryPass(s),"random pass");
                }
                Check(g.phase==2,"round terminates");
                Check(g.places[0]>0&&g.places[1]>0&&g.places[2]>0,"all ranked");
                Check(g.places[0]!=g.places[1]&&g.places[0]!=g.places[2]&&g.places[1]!=g.places[2],"distinct ranks");
                g.Deal();Check(g.phase==3&&g.pending==4&&g.pendingCount==2,"next round exchange");
                int a=g.turn;int[] chosen=new int[2];int q=0;for(int c=0;c<53&&q<2;c++)if(g.owners[c]==a)chosen[q++]=c;
                Check(Play(a,chosen)&&g.phase==1,"exchange ends");
            }
            for(int run=0;run<30;run++)
            {
                Reset(4095);g.phase=0;g.seats=new[]{1,0,0,0};int bots=1+run%3;
                for(int i=0;i<bots;i++)g.AddCpu();g.Deal();Check(g.HandCount(0)+g.HandCount(1)+g.HandCount(2)+g.HandCount(3)==53,"all 53 dealt");int steps=0;
                while(g.phase!=2&&steps++<2000)Check(g.CpuStep(),"CPU legal move/pending/pass");
                Check(g.phase==2,"CPU match terminates");
                for(int s=0;s<=bots;s++)Check(g.places[s]>=1&&g.places[s]<=bots+1,"CPU rank range");
                g.Deal();steps=0;while(g.phase==3&&steps++<5)Check(g.CpuStep(),"CPU card exchange");Check(g.phase==1,"CPU exchange completes");
            }
            return "PASS "+assertions+" assertions, 11 rule transitions, 20 basic rounds, 30 CPU rounds (2–4 seats)";
        }
        finally{UnityEngine.Object.DestroyImmediate(o);}
    }
}
