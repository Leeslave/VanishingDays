using System;
using System.Linq;
using UnityEngine;

namespace VanishingDays
{
    public partial class VanishingDaysGame
    {
        int livingFilter;
        string infoItem;
        static readonly string[] ItemArtIds={
            "spam","tuna","rice","burner","ramen","wood","tools","radio",
            "rice_meal","spam_meal","tuna_meal","ramen_meal","clothes","weapon","bandage","pills",
            "antiseptic","water","raw_water","cloth","scrap","cord","tape","club",
            "wrapped_club","nail_bat","pipe_club","scrap_spear","machete","hatchet","reinforced_blade","workbench",
            "water_filter","water_filter2","water_filter3","water_collector","bedroll","door1","door3","analog","digital"};
        Texture2D itemArt,snackArt;
        // The illustrated atlas has slightly different row heights; use its drawn cell boundaries.
        static readonly float[] ArtRows={0,.168f,.324f,.485f,.653f,.823f,1};
        void ItemPicture(string id,float x,float y,float w,float h,Transform parent=null)
        {
            if(itemArt==null) itemArt=Resources.Load<Texture2D>("Art/ItemAtlas");
            int index=Array.IndexOf(ItemArtIds,id=="door2"?"door1":id);
            int snack=Array.IndexOf(new[]{"chocolate","soda","energy_bar"},id);
            if(snack>=0 && snackArt==null) snackArt=Resources.Load<Texture2D>("Art/SnackAtlas");
            if(snack>=0 && snackArt!=null)
            {
                var image=Rect("Item illustration:"+id,parent??root,x,y,w,h).gameObject.AddComponent<UnityEngine.UI.RawImage>();
                image.texture=snackArt;image.raycastTarget=false;image.uvRect=new UnityEngine.Rect(snack/3f,0,1/3f,1);return;
            }
            if(itemArt==null || index<0) { ArtIcon(5,x,y,w,h,parent); return; }
            var picture=Rect("Item illustration:"+id,parent??root,x,y,w,h).gameObject.AddComponent<UnityEngine.UI.RawImage>();
            picture.texture=itemArt; picture.raycastTarget=false;
            int row=index/8;
            picture.uvRect=new UnityEngine.Rect(index%8/8f+.002f,1-ArtRows[row+1]+.002f,1/8f-.004f,ArtRows[row+1]-ArtRows[row]-.004f);
        }
        int LivingCategory(string id) => id.Contains("water")?1:3;
        void LivingFilters()
        {
            string[] names={"전체","정수 · 집수","문 · 보안","작업대 · 생활"};
            for(int i=0;i<names.Length;i++)
            {
                int selected=i;
                var button=Pressable(names[i],48+i*204,748,192,54,()=>{livingFilter=selected;craftOffsets[2]=0;craftScroll=null;Render();},true,livingFilter==i?Amber:Edge,livingFilter==i?EmberDim:Panel);
                Label(names[i],4,0,184,54,21,livingFilter==i?Amber:Paper,button.transform).alignment=TextAnchor.MiddleCenter;
            }
        }
        void ItemInfoPage()
        {
            var item=definitions.Item(infoItem);
            Label(item.name+" · 설명",48,462,804,60,36,Paper);
            ItemPicture(item.id,48,540,120,120);
            Label("이 물품으로 만들 수 있는 것",194,568,650,58,28,Amber);
            ScrollArea("Item uses",690,580,out var content); float y=12;
            Action<string,ItemStack[],int> recipe=(name,costs,hours)=>{
                Label(name,18,y,768,40,28,Paper,content);
                var label=Label(Cost(costs)+" · "+hours+"시간",18,y+48,768,90,22,Muted,content);
                float height=Mathf.Max(90,label.preferredHeight);label.rectTransform.sizeDelta=new Vector2(768,height);y+=height+78;
            };
            foreach(var r in definitions.recipes.Where(r=>state.CookingIngredients(r).Any(c=>c.id==infoItem))) recipe(r.name,state.CookingIngredients(r),r.hours);
            foreach(var r in definitions.crafting.recipes.Where(r=>r.ingredients.Any(c=>c.id==infoItem))) recipe(r.name,r.ingredients,r.hours);
            foreach(var r in definitions.security.Where(r=>r.ingredients.Any(c=>c.id==infoItem))) recipe(r.name,r.ingredients,r.hours);
            if(y==12) Label(item.consumable?"직접 먹거나 마실 수 있다.":item.injuriesTreated>0?"부상을 치료하는 의약품이다.":item.strength>0 || item.defense>0?"착용해서 힘이나 방어를 높인다.":item.id=="radio"?"방송과 아침 소식을 수신한다.":item.id=="burner"?"집에서 요리하거나 물을 끓일 때 사용하는 설비다.":"현재 제작법의 소모 재료로 쓰이지 않는다.",18,y,768,120,25,Muted,content);
            content.sizeDelta=new Vector2(804,Mathf.Max(580,y));
            Button("물품으로 돌아가기",48,1300,804,80,()=>{page="inventory";Render();},true,true);
        }
        void RequestDeparture(string destination)
        {
            selectedLocation=destination;
            if(state.CurrentLocation=="home" && destination!="home") {selectedInventoryItem=null;page="departure";Render();}
            else Apply(state.Travel(destination,definitions));
        }
        void DeparturePage()
        {
            Label("외출 준비 · "+definitions.Location(selectedLocation).name,48,462,804,60,34,Paper);
            Label("집의 물자는 가져가지 않으면 사용할 수 없다. 수량을 조정해 챙기자.",48,534,804,74,24,Muted);
            ScrollArea("Departure supplies",620,580,out var content);float y=8;
            foreach(var item in definitions.items.Where(x=>x.id!="burner" && (state.HomeStorage.Count(x.id)>0 || state.Backpack.Count(x.id)>0 || state.Equipment.Contains(x.id))))
            {
                string id=item.id;int pack=state.Backpack.Count(id),home=state.HomeStorage.Count(id);
                ItemPicture(id,12,y,64,64,content);
                Label(item.name+(state.Equipment.Contains(id)?" · 착용":""),92,y,400,38,25,Paper,content);
                Label("소지 "+definitions.Quantity(id,pack)+" · 집 "+definitions.Quantity(id,home),92,y+40,420,34,21,Muted,content);
                int unit=id=="rice"?100:1;
                Button("-"+(id=="rice"?"100g":"1"),548,y,110,64,()=>Apply(state.Transfer(id,false,Math.Min(unit,pack),definitions),"departure"),pack>0,false,content);
                Button("+"+(id=="rice"?"100g":"1"),674,y,110,64,()=>Apply(state.Transfer(id,true,Math.Min(unit,home),definitions),"departure"),home>0,true,content);
                y+=104;
            }
            content.sizeDelta=new Vector2(804,Mathf.Max(580,y));
            bool water=state.UnlimitedWater || state.Backpack.Count("water")>0;
            Label(water?"출발하면 소지품과 착용 장비만 사용할 수 있다.":"주의: 소지한 생수가 없다. 외출 중 식수가 부족할 수 있다.",48,1210,804,66,24,water?Muted:Amber);
            Button("준비 완료 · 출발",48,1300,500,80,()=>Apply(state.Travel(selectedLocation,definitions)),state.CanAct,true);
            Button("취소",570,1300,282,80,()=>{page="location";Render();});
        }
        void ShelterInstallations()
        {
            var facilities=state.InstalledFacilities.Where(id=>!id.StartsWith("water_filter") || id=="water_filter"+(state.WaterFilterLevel==1?"":state.WaterFilterLevel.ToString())).ToList();
            if(facilities.Count==0 && state.InstalledSecurity.Count==0) return;
            var card=Pressable("집에 설치한 설비",48,554,804,86,()=>{page="facilities";Render();},state.CanAct,Edge,new Color(Ink.r,Ink.g,Ink.b,.92f)).transform;
            for(int i=0;i<Math.Min(5,facilities.Count);i++) ItemPicture(facilities[i],12+i*76,8,68,68,card);
            Label("설치 설비 보기  ›",424,20,360,48,24,Paper,card);
        }
        void FacilitiesPage()
        {
            Label("집에 설치한 설비",48,462,804,60,36,Paper);
            ScrollArea("Installed facilities",548,720,out var content);float y=12;
            foreach(var id in state.InstalledFacilities)
            {
                var facility=definitions.crafting.Find(id);if(facility==null)continue;
                ItemPicture(id,18,y,124,124,content);
                Label(facility.name,170,y,610,42,28,Paper,content);
                Label(facility.description,170,y+50,610,130,22,Muted,content);y+=204;
            }
            foreach(var id in state.InstalledSecurity)
            {
                var device=definitions.Security(id);if(device==null)continue;
                ItemPicture(id,18,y,124,124,content);Label(device.name,170,y,610,44,28,Paper,content);
                Label("설치 완료 · 침입 저항 +"+device.protection,170,y+56,610,64,24,Muted,content);y+=154;
            }
            content.sizeDelta=new Vector2(804,Mathf.Max(720,y));
            Button("생활 화면",48,1300,804,80,()=>{page="home";Render();},true,true);
        }
#if UNITY_EDITOR || VD_UI_VERIFICATION
        void CaptureQa(string path)
        {
            // Render to a texture so verification also works with an occluded Windows player.
            var canvas=root.GetComponentInParent<Canvas>();
            var host=new GameObject("QA render camera",typeof(Camera));var camera=host.GetComponent<Camera>();
            camera.orthographic=true;camera.orthographicSize=800;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Ink;
            camera.transform.position=new Vector3(0,0,-10);camera.enabled=false;
            var target=new RenderTexture(900,1600,24);target.Create();
            var oldMode=canvas.renderMode;var oldCamera=canvas.worldCamera;
            var scaler=canvas.GetComponent<UnityEngine.UI.CanvasScaler>();bool scaling=scaler!=null && scaler.enabled;float oldScale=canvas.scaleFactor;
            if(scaler!=null)scaler.enabled=false;canvas.scaleFactor=1;
            canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=1;
            camera.targetTexture=target;Canvas.ForceUpdateCanvases();
            UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(camera,new UnityEngine.Rendering.RenderPipeline.StandardRequest{destination=target});
            var previous=RenderTexture.active;RenderTexture.active=target;
            var texture=new Texture2D(900,1600,TextureFormat.RGB24,false);texture.ReadPixels(new UnityEngine.Rect(0,0,900,1600),0,0);texture.Apply();
            System.IO.File.WriteAllBytes(path,texture.EncodeToPNG());
            RenderTexture.active=previous;canvas.renderMode=oldMode;canvas.worldCamera=oldCamera;
            camera.targetTexture=null;target.Release();Destroy(target);Destroy(texture);Destroy(host);
            canvas.scaleFactor=oldScale;if(scaler!=null)scaler.enabled=scaling;Canvas.ForceUpdateCanvases();
        }
        System.Collections.IEnumerator SmokeQa()
        {
            string folder=System.IO.Path.GetFullPath("Builds/QaVerification");System.IO.Directory.CreateDirectory(folder);
            state=SurvivalState.NewGame(GameDifficulty.EASY,definitions,123);
            state.Story.OpeningCompleted=true;state.PlayerNickname="한글★_!";state.NeighborIntroductionSeen=true;
            state.HomeStorage.Add("tape",8);state.HomeStorage.Add("wood",20);state.HomeStorage.Add("cloth",20);
            state.HomeStorage.Add("tools",20);state.HomeStorage.Add("scrap",20);state.HomeStorage.Add("cord",20);
            Render();yield return null;
            ClickGameButton("제작");ClickGameButton("생활용품");ClickGameButton("정수 · 집수");
            if(root.GetComponentsInChildren<UnityEngine.UI.Text>().Any(x=>x.text=="작업대" || x.text=="보강 침구"))throw new Exception("Living filter leaked other categories.");
            yield return null;yield return new WaitForEndOfFrame();CaptureQa(System.IO.Path.Combine(folder,"01-WATER-CRAFT.png"));yield return null;
            page="inventory";selectedInventoryItem="tape";Render();ClickGameButton("설명");
            if(!root.GetComponentsInChildren<UnityEngine.UI.Text>().Any(x=>x.text=="헝겊 곤봉") || !root.GetComponentsInChildren<UnityEngine.UI.Text>().Any(x=>x.text=="붕대 만들기"))throw new Exception("Tape recipe uses missing.");
            yield return null;yield return new WaitForEndOfFrame();CaptureQa(System.IO.Path.Combine(folder,"02-ITEM-USES.png"));yield return null;
            int hour=state.hour;selectedLocation="store";RequestDeparture(selectedLocation);
            if(page!="departure" || state.hour!=hour)throw new Exception("Departure preparation spent time.");
            yield return null;yield return new WaitForEndOfFrame();CaptureQa(System.IO.Path.Combine(folder,"03-DEPARTURE.png"));yield return null;
            state.Transfer("tuna",true,1,definitions);ClickGameButton("준비 완료 · 출발");
            state.PendingEncounter=null;page="inventory";selectedInventoryItem=null;Render();
            if(root.GetComponentsInChildren<UnityEngine.UI.Button>().Any(x=>x.name=="물품:tape") || root.GetComponentsInChildren<UnityEngine.UI.Text>().Any(x=>x.text.Contains(" · 집 ")))throw new Exception("Away inventory revealed home storage.");
            yield return null;yield return new WaitForEndOfFrame();CaptureQa(System.IO.Path.Combine(folder,"04-CARRIED-ONLY.png"));yield return null;
            state.CurrentLocation="home";state.AwayFromHome=false;state.hour=8;page="sleep";Render();
            int before=state.day*24+state.hour;ClickGameButton("8시간 잔다");
            if(state.day*24+state.hour-before!=8)throw new Exception("Actual eight-hour button advanced the wrong time.");
            state.InstalledFacilities.AddRange(new[]{"workbench","water_filter","water_collector","bedroll"});page="home";Render();
            ClickGameButton("집에 설치한 설비");
            if(!root.GetComponentsInChildren<UnityEngine.UI.RawImage>().Any(x=>x.name=="Item illustration:water_filter"))throw new Exception("Installed facility artwork missing.");
            yield return null;yield return new WaitForEndOfFrame();CaptureQa(System.IO.Path.Combine(folder,"05-FACILITIES.png"));yield return null;
            state.Story.HasHeardSPBroadcast=true;state.Story.SupplyRumorUnlocked=true;state.LastRadioBroadcastId=definitions.radioStory.generalBroadcast.id;
            page="journal";Render();ClickGameButton("정보 카드 · 라디오 전문");
            // The receiver log lays a broadcast out line by line, so every line of the first broadcast must be on screen.
            var shown=root.GetComponentsInChildren<UnityEngine.UI.Text>().Select(x=>x.text).ToList();
            if(!ScriptBlocks.Parse(definitions.radioStory.firstBroadcast.body).Where(x=>x.Kind!=ScriptBlockKind.Noise).All(b=>shown.Contains(b.Text)))throw new Exception("First broadcast was lost after another broadcast.");
            yield return null;yield return new WaitForEndOfFrame();CaptureQa(System.IO.Path.Combine(folder,"06-RADIO-ARCHIVE.png"));yield return null;
            Debug.Log("VD_QA_UI_OK: crafting filters, recipe uses, preparation without time cost, carried-only inventory, eight-hour sleep, facility art and permanent radio transcript.");Application.Quit();
        }
#endif
    }
}
