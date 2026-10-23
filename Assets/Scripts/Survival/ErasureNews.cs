using System;
using System.Collections.Generic;
using UnityEngine;

namespace VanishingDays
{
    [Serializable] public class ErasureLocationDefinition
    {
        public string id, region, locality, kind, site, report;
        public float latitude, longitude;
        public string Title => locality+" · "+site;
    }
    [Serializable] public class ErasureNewsCatalog
    {
        public ErasureLocationDefinition[] locations;
        public ErasureLocationDefinition Find(string id) => Array.Find(locations,x=>x.id==id);
        public static ErasureNewsCatalog Load()
        {
            var asset=Resources.Load<TextAsset>("Survival/ErasureNews");
            if(asset==null) throw new InvalidOperationException("Erasure news catalog is missing.");
            var data=JsonUtility.FromJson<ErasureNewsCatalog>(asset.text); data.Validate(); return data;
        }
        public void Validate()
        {
            if(locations==null || locations.Length!=50) throw new InvalidOperationException("Expected 50 erasure locations.");
            int seoul=0, towns=0; var ids=new HashSet<string>();
            foreach(var place in locations)
            {
                if(place==null || string.IsNullOrWhiteSpace(place.id) || !ids.Add(place.id) || string.IsNullOrWhiteSpace(place.locality) || string.IsNullOrWhiteSpace(place.site) || string.IsNullOrWhiteSpace(place.report) || float.IsNaN(place.latitude) || float.IsNaN(place.longitude) || place.latitude<33 || place.latitude>39 || place.longitude<125 || place.longitude>130) throw new InvalidOperationException("Invalid erasure location.");
                if(place.region=="서울") { seoul++; if(place.kind!="shelter" && place.kind!="vacant") throw new InvalidOperationException("Invalid Seoul erasure scope."); }
                else { towns++; if(place.kind!="town" || !place.locality.EndsWith("읍")) throw new InvalidOperationException("Rural reports must describe a town."); }
            }
            if(seoul!=20 || towns!=30) throw new InvalidOperationException("Expected 20 Seoul sites and 30 rural towns.");
        }
    }
    [Serializable] public class ErasureReport { public int day; public string locationId=""; }
    public partial class SurvivalState
    {
        public List<ErasureReport> ErasureHistory=new List<ErasureReport>();
        public int LastErasureDay, ErasureReadDay;
        public uint ErasureRandomState=2463534242;
        public bool ErasureNewsEnabled => Story.HasRadio && Story.CrowdIncidentResolved;
        public bool HasUnreadErasureNews => ErasureHistory.Exists(x=>x.day>ErasureReadDay);
        public ErasureReport NextUnreadErasureNews => ErasureHistory.Find(x=>x.day>ErasureReadDay);
        public void InitializeErasureNews()
        {
            ErasureHistory=new List<ErasureReport>(); LastErasureDay=day; ErasureReadDay=day;
            ErasureRandomState=RandomState^0x9E3779B9u;
            if(ErasureRandomState==0) ErasureRandomState=1;
        }
        public void GenerateDailyErasureNews(SurvivalDefinitions data)
        {
            if(!ErasureNewsEnabled || day<=LastErasureDay) return;
            var used=new HashSet<string>(); foreach(var report in ErasureHistory) if(!string.IsNullOrEmpty(report.locationId)) used.Add(report.locationId);
            var available=new List<ErasureLocationDefinition>(); foreach(var place in data.erasureNews.locations) if(!used.Contains(place.id)) available.Add(place);
            string id="";
            if(available.Count>0)
            {
                uint value=ErasureRandomState; value^=value<<13; value^=value>>17; value^=value<<5; ErasureRandomState=value==0?1:value;
                id=available[(int)(ErasureRandomState%(uint)available.Count)].id;
            }
            var bulletin=new ErasureReport { day=day,locationId=id };
            ErasureHistory.Add(bulletin); LastErasureDay=day;
            string before=lastReport; Note("[소멸 속보] "+ErasureHeadline(bulletin,data)); lastReport=before;
        }
        public string ErasureHeadline(ErasureReport report,SurvivalDefinitions data) => string.IsNullOrEmpty(report.locationId)?"새롭게 확인된 소멸 제보 없음":data.erasureNews.Find(report.locationId).Title;
        public string ErasureBody(ErasureReport report,SurvivalDefinitions data)
        {
            if(string.IsNullOrEmpty(report.locationId)) return "[SP 일일 수신 기록]\n\n지금까지 접수한 지역 외에 새롭게 확인된 소멸 제보는 없습니다.\n\n제보가 없다는 것이 아무 일도 없었다는 뜻은 아닙니다. 이미 기록된 지역을 다시 소멸한 것으로 보도하지 않겠습니다.";
            var place=data.erasureNews.Find(report.locationId);
            string scale=place.kind=="town"?"읍 단위 소멸 제보":place.kind=="shelter"?"인파가 밀집한 임시 대피소 소멸 제보":"사람들이 떠난 빈 건물 소멸 제보";
            return "[SP 일일 수신 기록]\n"+place.Title+"\n"+scale+"\n\n"+place.report+"\n\n통신이 끊긴 구역의 인명 상황은 확인되지 않았습니다. 주변을 지날 때는 접근을 삼가십시오.";
        }
        public void AcknowledgeErasureNews()
        {
            var report=NextUnreadErasureNews; if(report!=null) ErasureReadDay=report.day;
        }
    }
}
