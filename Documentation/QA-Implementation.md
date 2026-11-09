# QA 반영 · 2026-11-03

- 이름: 한글, 특수문자와 이름 안 공백을 허용(공백만·제어문자·12자 초과 제외). WebGL은 브라우저 입력칸을 사용하여 한글 조합 중 Enter 제출을 차단하고 조합 완료 뒤 Unity로 전달한다. Windows는 기존 IME 브리지와 함께 레거시 텍스트 이벤트를 지원하도록 입력 백엔드를 Both로 설정했다.
- 제작: 음식/무기/생활용품 아래 생활용품을 전체·정수/집수·문/보안·작업대/생활로 필터링한다. 정수기 단계도 같은 분류에 모인다.
- 물품: 전체 34개 아이템의 고유 그림을 물품, 준비 화면, 제작 결과에 연결. 아이템 선택 → 설명에서 요리·제작·보안의 실제 재료 데이터를 역조회하여 만들 수 있는 물품과 전체 필요 재료·시간을 표시한다.
- 외출: 집에서 이동하기를 누르면 준비 화면이 열린다. 소지/창고 물자를 확인하며 1개(쌀 100g)씩 챙기거나 돌려놓고 준비 완료 후 출발한다. 준비·취소는 시간을 소비하지 않는다. 기존 물품 화면에서 정확한 수량도 입력할 수 있다.
- 외부 물품/식수 화면은 현재 소지품·착용 장비만 표시한다. 창고에만 있는 아이템은 숨긴다.
- 기록 및 집의 정보 카드에서 가락시장 첫 방송 전문을 다시 읽는다. 이후 일반 방송을 들어도 첫 원문을 보존한다.
- 2/6/8시간 수면에 기상 예정 시각을 표시한다. 명시적 시간이 지정되면 주기 넘기기보다 우선한다. 하단 시간 넘기기 버튼도 수면 시간 선택으로 연결했다. 기존 8시간 수면 산식 자체는 8시간이었다.
- 설치한 작업대·정수기·집수통·침구와 보안 설비를 집의 그림 목록으로 확인한다. 집에 설치 설비 미리보기와 열기 버튼을 표시한다.
- 외출과 제작 버튼 위치 교환: 외출은 하단 보조 버튼, 제작은 하단 두 번째 탭.
- 숫자 수량 입력 중 1/2/3 단축키가 생존 행동을 실행하지 않도록 차단한다.

## 검증

- Unity 메뉴: Vanishing Days/Validate QA fixes. 기존 규칙 검사를 포함하고 난이도 3종 × 시작 시각 6종 × 수면 3종의 실제 시간 경과 54개를 검사한다.
- UI 검증 빌드: -vdQa. 제작 필터, 아이템 용도, 외출 준비, 외부 소지품, 실제 8시간 버튼, 설비 그림, 라디오 원문을 확인하고 스크린샷을 남긴다.
- node Tests/NicknameInput.test.cjs: 브라우저 한글 조합·특수문자 전달·Enter·크기·정리.
- node Tests/MobileInputViewport.test.cjs 및 node Tests/BrowserSaveBackup.test.cjs.
- 실제 OS 한글 IME와 iOS/Android 소프트 키보드 동작은 기기에서 별도 확인이 필요하다.

검사 결과: 기존 Unity 게임 규칙 검사, 수면 54개 시간 조합과 명시적 시간 우선 규칙, 브라우저 JavaScript 검사 3종, Windows 검증 빌드 및 `-vdQa` 실제 버튼 검사 통과. 외출 준비·소지품·제작 필터·물품 설명·설비·라디오의 6개 화면을 렌더 텍스처로 캡처해 시각 확인했다. 검증 중 필터 글자 잘림과 그림 셀 경계 겹침을 수정했다. WebGL 전용 입력 코드의 조건부 C# 컴파일도 WebGL 모듈 참조로 확인했다.

검증 스크린샷: `Builds/QaVerification/`. 검증 빌드: `Builds/QaValidationProject/Builds/WindowsVerification/VanishingDays.exe`. 일반 실행은 게임을 시작하며, `-vdQa` 인자를 주면 QA 자동 검사를 실행한다. 기존 `Builds/Windows`와 웹 배포본은 교체하지 않았다.

실제 오프닝 UI의 한글 문자 입력·IME 조합 표시·닉네임 저장도 `-vdSmoke -vdOpeningOnly`로 통과했다(`VD_TUTORIAL_OPENING_UI_OK`).

### 에디터 닉네임 한글 입력 후속 수정

에디터 Play에서 한글 입력이 여전히 안 된다는 재현 제보에 따라 닉네임 입력칸의 문자 수신 경로를 수정했다. 기존 IME 브리지는 조합 문자열만 전달했으며, uGUI 기본 입력칸은 확정 문자를 레거시 `Event.PopEvent`에서 읽고 있었다. 앞선 검사는 `ProcessEvent`로 문자를 직접 넣어 이 누락을 검출하지 못했다.

네이티브 닉네임 입력칸은 `Keyboard.onTextInput`으로 확정 문자를 수신한다. 레거시 문자 이벤트를 동시에 읽지 않아 중복 입력을 막고, 조합 중 Enter·Backspace는 IME가 처리한다. 확정 문자를 넣을 때는 조합 문자열을 잠시 분리해 커서 위치에 잘못 더해지지 않도록 한다. 선택·커서 이동·복사·붙여넣기·삭제는 Input System 키 이벤트로 처리한다. 웹은 기존 브라우저 입력칸을 유지한다.

`-vdNicknameInput` 검사는 실제 Input System의 IME·Text 이벤트를 사용해 조합 표시, 한글 확정, 다음 음절과 같은 프레임에 발생하는 입력, 특수문자, 커서 삽입, Backspace, 닉네임 저장을 검사한다. 에디터 자동 검사 진입점은 `VanishingDays.Editor.NicknameInputChecks.Play`이다. 기존 오프닝 검사도 확정 문자 주입을 `QueueTextEvent`로 교체했다.

검증 결과: 격리된 Unity 프로젝트의 실제 Editor Play 모드에서 `activeInputHandler: 1`(Input System만 사용)로 위 입력 검사를 통과했다(`VD_NATIVE_NICKNAME_OK`, `Builds/Validation/nickname-editor.log`). 헤드리스 검증에서는 Game 뷰 포커스 제한을 검증 프로젝트에서만 해제했다. 원본 프로젝트의 포커스 설정은 변경하지 않았다. 현재 실행 중인 Play는 종료 후 다시 시작해야 새 입력칸 컴포넌트가 생성된다.

## 그림

### 선택 객체 오류·생활 안내·식수 화면 후속 수정

- 보고된 `MissingReferenceException`의 스택은 `VanishingDaysGame.Update`의 파괴된 선택 객체 `GetComponent` 호출이었다. Unity 객체에는 C# null 조건 연산자 대신 Unity의 유효성 비교를 적용하고, 화면 재생성 전에 EventSystem 선택을 해제했다. IME 조합 표시 코드에도 같은 유효성 검사를 적용했다.
- 완료한 생활 안내를 다음 미완료 단계까지 자동으로 넘긴다. 재실행 시에도 완료한 화면 기록을 유지하고, 불필요한 수동 다음 확인 버튼을 제거했다. 확인 중인 물품·식수 화면이나 이웃 대화는 그대로 읽을 수 있으며 안내에 돌아오면 다음 단계가 표시된다.
- 식수 화면을 마시기 / 물 확보 / 끓이기·정수 / 설비로 분류했다. 생수·목마름·혼탁한 물 요약, 작업별 결과·소요 시간, 집수 시간 3개를 한 카드로 정리했다. 외출 중에는 소지 생수만 표시한다.
- 에디터 Play 검증에서 안내 86개·식수 29개 검사 및 실제 버튼 시나리오를 통과했다. 선택된 GameObject를 파괴한 뒤 Update를 실행하는 회귀 검사도 통과했으며, 검증 로그에 MissingReferenceException은 없었다. 식수 화면 5종의 렌더 캡처를 확인했다.
- 로그: `Builds/Validation/interface-repair-editor.log`. 캡처: `Builds/QaValidationProject/Builds/InterfaceRepairVerification/`.

기본 내장 image_gen 사용. 프로젝트 내 최종 파일:
- Assets/Resources/Art/ItemAtlas.png
- Assets/Resources/Art/SnackAtlas.png

ItemAtlas 최종 프롬프트:

Create a production game inventory sprite atlas, square PNG, EXACT 8 columns by 6 rows, 48 equal square cells, no borders or text. Each object fully inside its own cell with 15% padding, centered, consistent scale, muted warm ivory charcoal pencil and realistic hand drawn survival game illustration on uniform very dark blue grey #121a1c. Clear recognizable silhouettes, sparse amber accents, Korean post apocalypse household scavenging. Read order LEFT TO RIGHT TOP TO BOTTOM: row1 rectangular canned luncheon meat; round tuna tin with fish pictogram; tied rice sack; portable single burner gas stove; instant ramen packet; three wood planks; hammer and screwdriver; portable analog radio. Row2 bowl white rice; rice bowl with luncheon meat slices; rice bowl with tuna flakes; steaming noodle bowl with chopsticks; folded jacket; kitchen knife; rolled medical bandage; blister pack pills. Row3 disinfectant bottle with medical cross; clear drinking water bottle blue cap; dirty water bottle muddy brown; folded cloth rags; metal scrap nuts and plates; coiled rope; grey adhesive duct tape roll WITH VISIBLE hollow center; plain wooden club. Row4 cloth wrapped wooden club; nail studded baseball bat; steel pipe; scrap metal spear; improvised machete; small hatchet; reinforced machete; wooden workbench with vise. Row5 crude water filter bucket with cloth cone; upgraded two stage water purifier; advanced three stage water purifier; rainwater collection barrel; bedroll pillow and blanket; reinforced door wooden crossbar; padlock and chain; door alarm bell. Row6 CCTV camera; motion sensor; solar panel; improvised wire fence; cooking pot with lid; water kettle; sturdy backpack; notebook. EXACT one object per cell, no omissions, no labels, no decorative scene, all 48 cells filled.

SnackAtlas 최종 프롬프트:

A game inventory sprite sheet with exactly 3 columns and 1 row of equal square cells, wide landscape canvas 3:1. Muted warm ivory charcoal pencil realistic hand drawn Korean post apocalypse scavenging game, sparse amber accents. Uniform very dark blue grey background #121a1c. NO text, NO labels. LEFT cell a partly unwrapped chocolate bar with visible chocolate squares in brown foil; CENTER cell a red pull tab soda can with condensation; RIGHT cell a compact granola energy bar partly in beige wrapper revealing oats nuts. Center each item within its cell, generous 15 percent padding, each completely separate. Match worn household survival illustrations.


## 21일차 플레이 피드백

- 대면 장면은 초상 비율을 보존하고 대사판을 아래쪽에 반투명으로 배치했다. 생활 화면은 스크롤되는 행동 목록을 제공하고, 라디오를 얻어도 03번 제작을 유지한다. 설치 설비는 옆집·동행과 같은 크기의 버튼이다.
- 작업대 Lv. 1은 중급 둔기와 간이 정수기, Lv. 2는 칼날 계열과 그럴듯한 정수기, Lv. 3은 강화 칼날과 괜찮은 정수기를 연다. 정수 한 묶음은 1시간이고, 집수통 회수는 0시간이다. 기존에 설치한 설비는 이전 기록에서 계속 사용할 수 있다.
- 보통 이동에서 학교와 주요 거점까지 최대 2시간, 학교 수색은 회당 2시간으로 조정했다. 학교 기준 08:00 출발 → 수색 두 번 → 16:00 귀가다. 동행 약속에는 다음 이야기 장소와 순찰 날짜를 함께 표시한다.
- 물품 전달에는 보상과 신뢰를 명시하고, 현장 확인은 철물점·학교·약국 등 장소별 일회성 요청으로 처리한다.
- 무기를 들면 위협과 공격을 선택할 수 있다. 위협은 기존 탈출 확률에 25%p를 더하며 실패 시 물자를 잃는다. 공격은 탈출을 보장하고, 무기의 힘에 따라 상대의 부상과 재현의 정신력 손실을 구분한다.
- 첫 생존 안내에서 버튼 설명과 시계 정지 상태의 수면 연습을 제공한다. 집수통을 새로 설치하면 식수 관리 → 물 확보 → 집수 시간 선택을 실제 버튼으로 따라간다. 집수 완료 알림 뒤에는 기존 화면이 보인다.
- 시작·이어하기·게임 메뉴에서 별도 `StoryReader.unity`를 연다. 프롤로그, 1~5막과 분기 결과, 결말을 스크롤해 읽을 수 있다. 현재 생존 기록을 따로 열어 편집할 필요가 없다.
- Unity 규칙 검사 16종, 오프닝 126개 단언, Windows UI 검사 13종, 브라우저 저장·입력 검사 3종을 통과했다. 검증 플레이어에서 스토리 열람 전후의 시간·난수·저장 파일이 동일함을 확인했다.
- 일반 Windows와 WebGL 빌드를 완료했다. WebGL 로더 참조, Gzip 폴백, WebAssembly 유효성, 브라우저 저장 복구 설정을 확인했다.
