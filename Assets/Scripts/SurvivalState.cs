using System;
using System.Collections.Generic;
using UnityEngine;

namespace VanishingDays
{
    [Serializable]
    public class District
    {
        public string name, description, clue;
        public int food, water, scrap, medicine, silence;
        public bool erased;
        public District(string n, string d, string c, int f, int w, int s, int m, int age)
        { name=n; description=d; clue=c; food=f; water=w; scrap=s; medicine=m; silence=age; }
    }

    [Serializable]
    public class SurvivalState
    {
        public int day=1, hour=8, health=85, food=4, water=4, scrap=3, medicine=1;
        public int protectedDistrict=-1, investigated;
        public bool filter, garden, beacon, finished, won;
        public List<District> districts=new List<District>();
        public List<string> journal=new List<string>();
        public string lastReport="어젯밤, 맞은편 아파트가 사라졌다. 무너진 소리는 없었다. 그 자리에 있던 하늘만 조금 더 넓어졌다.";

        public static SurvivalState NewGame()
        {
            var s=new SurvivalState();
            s.districts.Add(new District("을지로 철물점", "셔터 아래로 녹슨 공구가 보인다. 주인 없는 라디오에서는 같은 잡음이 반복된다.", "벽시계의 초침이 간헐적으로 멈춘다.",0,0,4,0,1));
            s.districts.Add(new District("종로 약국", "흰 가운이 의자에 걸려 있다. 진열장 안쪽에는 아직 뜯지 않은 구급 상자가 남아 있다.", "간판에서 글자 하나가 흐려졌다.",0,1,1,2,3));
            s.districts.Add(new District("청계천 급수소", "물소리는 여전하다. 다만 수면에 비친 건물의 수가 실제보다 하나 적다.", "물소리 사이에 긴 침묵이 끼어든다.",0,4,1,0,2));
            s.districts.Add(new District("충무로 편의점", "문이 열리자 종이 울린다. 전기는 끊겼지만 선반에는 통조림이 남아 있다.", "영수증에 인쇄된 주소가 번졌다.",4,2,0,0,0));
            s.districts.Add(new District("세운상가", "복도마다 전선이 늘어져 있다. 누군가 조립하던 수신기에 작은 불이 들어온다.", "스피커에서 자신의 발소리가 한 박자 늦게 들린다.",0,0,5,0,4));
            s.districts.Add(new District("남산 온실", "깨진 유리 지붕 아래로 새싹이 올라온다. 이곳의 흙은 아직 축축하다.", "유리창 너머 풍경에 잠깐 빈칸이 생긴다.",3,1,1,0,1));
            s.districts.Add(new District("서울역 대합실", "출발하지 않는 열차를 기다리던 흔적들. 긴 의자 아래 배낭 하나가 놓여 있다.", "안내 방송에서 역 이름만 들리지 않는다.",2,2,2,0,5));
            s.districts.Add(new District("서촌 기록관", "젖은 문서 더미에 도시의 옛 지도가 묻혀 있다. 지도에는 사라진 골목도 남아 있다.", "대장에 적힌 방문 날짜가 유난히 오래됐다.",1,1,2,1,2));
            s.Note("기록을 시작한다. 오늘은 을지로의 옥탑방에서 눈을 떴다.");
            return s;
        }

        public bool CanAct(int hours) { return !finished && hour+hours<=24; }
        public void Note(string text) { journal.Insert(0, "DAY "+day.ToString("00")+" · "+hour.ToString("00")+":00  "+text); }
        public string Visit(int index, bool thorough)
        {
            if(index<0 || index>=districts.Count) return "도착할 수 없는 곳이다.";
            var d=districts[index];
            int cost=thorough ? 4:3;
            if(d.erased || !CanAct(cost)) return "오늘은 더 이상 탐험할 수 없다.";
            hour+=cost;
            int before=d.silence;
            d.silence=0;
            int f=thorough?d.food:Mathf.Min(d.food,1), w=thorough?d.water:Mathf.Min(d.water,1);
            int sc=thorough?d.scrap:Mathf.Min(d.scrap,1), m=thorough?d.medicine:0;
            food+=f; water+=w; scrap+=sc; medicine+=m;
            d.food-=f; d.water-=w; d.scrap-=sc; d.medicine-=m;
            int injury=thorough ? 6:0;
            health=Mathf.Max(0,health-injury);
            string result=d.name+"에서 식량 +"+f+", 식수 +"+w+", 부품 +"+sc+", 약품 +"+m+".";
            if(thorough) result+=" 잔해를 헤치느라 체력 6을 잃었다.";
            result+="\n도착 전에는 "+before+"일째 사람의 흔적이 없었다. 돌아갈 때에는 시계가 다시 움직였다.";
            Note(result.Replace("\n"," "));
            CheckDeath();
            return result;
        }

        public string Observe(int index)
        {
            if(index<0 || index>=districts.Count || districts[index].erased || !CanAct(1)) return "지금은 관찰할 수 없다.";
            hour++; investigated++;
            var d=districts[index];
            string text=d.name+"을 멀리서 관찰했다. "+d.clue+" 마지막으로 사람이 다녀간 지 "+d.silence+"일. 관찰만으로는 안쪽의 시계가 다시 움직이지 않았다.";
            Note(text); return text;
        }

        public string Craft(int recipe)
        {
            int[] costs={4,5,3};
            if(recipe<0 || recipe>2 || !CanAct(2) || scrap<costs[recipe]) return "시간이나 부품이 부족하다.";
            if((recipe==0 && filter)||(recipe==1 && garden)||(recipe==2 && beacon)) return "이미 제작한 설비다.";
            hour+=2; scrap-=costs[recipe];
            string text;
            if(recipe==0) { filter=true; text="간이 정수기를 만들었다. 매일 아침 식수 2를 얻는다."; }
            else if(recipe==1) { garden=true; text="옥상 텃밭을 만들었다. 매일 아침 식량 2를 얻는다."; }
            else { beacon=true; text="기록 송신기를 만들었다. 매일 한 장소에 기록을 송신해 자정의 소멸을 막을 수 있다."; }
            Note(text); return text;
        }

        public string Protect(int index)
        {
            if(!beacon || finished || index<0 || index>=districts.Count || districts[index].erased) return "기록 송신기가 필요하다.";
            protectedDistrict=index;
            string text=districts[index].name+"에 기록을 송신한다. 오늘 자정까지 보존된다.";
            Note(text); return text;
        }

        public string Heal()
        {
            if(medicine<=0 || !CanAct(1) || health>=100) return "사용할 약품이 없거나 치료가 필요하지 않다.";
            medicine--; hour++; health=Mathf.Min(100,health+25); Note("구급약을 사용했다. 체력 +25."); return "상처를 소독했다. 체력 +25. 아직 손끝에 감각이 남아 있다.";
        }

        // Stable discoverable rule: the least recently visited unprotected place disappears.
        // Observing from afar does not refresh a place. Stable index order resolves ties.
        public int NextErasure()
        {
            int target=-1;
            for(int i=0;i<districts.Count;i++)
                if(!districts[i].erased && i!=protectedDistrict && (target<0 || districts[i].silence>districts[target].silence)) target=i;
            return target;
        }

        public string Midnight()
        {
            if(finished) return lastReport;
            hour=24;
            int target=NextErasure();
            string report="자정. ";
            if(target>=0)
            {
                var d=districts[target]; d.erased=true;
                report+=d.name+"이 사라졌다. 마지막 방문으로부터 "+d.silence+"일이 지나 있었다. 지도 위에 빈칸이 하나 늘었다.";
            }
            if(protectedDistrict>=0) report+=" "+districts[protectedDistrict].name+"의 기록은 남았다.";
            food-=2; water-=2;
            int damage=(food<0 ? 22:0)+(water<0 ? 28:0);
            health=Mathf.Max(0,health-damage); food=Mathf.Max(0,food); water=Mathf.Max(0,water);
            if(damage>0) report+=" 부족한 식량과 식수로 체력 "+damage+"을 잃었다.";
            Note(report);
            foreach(var d in districts) if(!d.erased) d.silence++;
            protectedDistrict=-1;
            if(health<=0) { finished=true; won=false; report+="\n더 이상 펜을 잡을 힘이 없다. 일지는 여기서 멈춘다."; }
            else if(day>=7) { finished=true; won=true; report+="\n일곱 번째 밤을 넘겼다. 도시는 작아졌지만 나는 남았다. 누군가 다시 이 일지를 펼칠 수 있도록, 다음 장은 비워 둔다."; }
            else { day++; hour=8; if(filter) water+=2; if(garden) food+=2; report+="\n아침이 왔다. "+(filter?"정수기에서 식수 +2. ":"")+(garden?"텃밭에서 식량 +2. ":"")+"오늘도 남은 곳들을 기록한다."; }
            lastReport=report; return report;
        }
        void CheckDeath() { if(health<=0) { finished=true; won=false; lastReport="무너진 계단 앞에서 기록이 멈췄다."; } }
    }
}
