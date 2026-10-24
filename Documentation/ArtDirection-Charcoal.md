# 목탄·흑연 아트 방향

사용자 요청: AI 렌더처럼 매끈한 질감을 줄이고, This War of Mine이 연상되는 거친 수작업 생존 게임 이미지로 수정한다.

무채색, 거친 목탄과 연필 선, 문질러 지운 밝은 부분, 불균일한 해칭, 차가운 자연광을 사용한다. 은신처는 민간인이 쓰는 소박한 자취방으로 단순화하고, 작은 아이콘은 어두운 바탕에 밝은 윤곽으로 읽히도록 한다. 기존 아틀라스의 칸 배치와 물품 종류는 유지한다.

## 파일

- `Assets/Resources/Art/SurvivalAtlas.png`: 은신처 배경·생존용품 아이콘 8종.
- `Assets/Resources/Art/SeoulErasure.png`: 오프닝·탐색 배경의 서울 풍경.
- 수정 전 이미지는 `Builds/ArtBeforeCharcoal/`에 보관한다.

내장 imagegen의 이미지 편집 모드를 사용한다. 원작 게임 이미지나 로고는 사용하지 않는다.

## 적용 확인

검증용 플레이어에서 은신처와 작은 아이콘의 표시·배치 및 기존 식수·수면 동작을 확인했다(`Builds/CharcoalPlayerUI.log`, `VD_WATER_ACCESS_UI_OK`). 적용 화면은 `Builds/CharcoalVerification/03-WATER-ACCESS.png`에 보관한다.

일반 Windows와 WebGL 빌드 성공. 새 웹 빌드를 `sites/vanishing-days-web/dist`에 반영하고 gzip 해제와 WebAssembly 유효성, 브라우저 저장 동기화 설정을 확인했다. 실서버에는 게시하지 않았다.

## 서울 풍경 편집 프롬프트

Use case: style-transfer. Edit target is the supplied Seoul skyline image. Repaint it from scratch as an expressive hand drawn black-and-white charcoal and dry graphite illustration for a bleak civilian survival game, with the subdued human fragility and gritty editorial drawing atmosphere associated with This War of Mine. Preserve the panoramic landscape format and broad scene identity: Korean low-rise roofs, tangled utility cables, distant apartment slabs and Namsan tower; a tiny desk with an open diary and worn kettle at the left foreground; the city disappearing into blank rubbed paper toward the right. STRICT neutral black, charcoal gray, dirty chalk white; no amber lamps, no blue, no orange, no attractive cinematic glow. Simplify architecture into believable uneven silhouettes with a handful of carefully observed details. Real pencil pressure variations, broken ink contours, coarse paper grain, uneven crosshatching, broad matte charcoal shadows, eraser marks and unfinished paper. Cold overcast daylight, heavy air, restrained contrast with readable middle grays. A scanned artist's illustration, not glossy photorealism, not a 3D render, not a photographic pencil-effect filter. Avoid excessive miniature detail, spectacular sky, swirly painterly noise, repeated windows, melted objects, game screenshots or existing copyrighted art. No text, letters, logo, watermark, characters or UI.

## 아틀라스 편집 프롬프트

Use case: style-transfer. Edit the supplied game texture atlas. Preserve EXACT square format and atlas layout: top 50% one apartment scene, bottom 50% exactly 4 columns by 2 rows of square inventory tiles, same icon subjects in same positions. Repaint ALL imagery in a gritty This War of Mine inspired civilian survival visual language: authentic scanned charcoal, dry graphite, scratchy ink, rubbed-out highlights on coarse gray paper, imperfect economical human marks, broad quiet shapes and selective detail, harsh natural overcast light. STRICT monochrome black, ash gray, dirty off-white; remove amber lighting, metallic sparkle, blue bottle cap, glossy 3D render surfaces and cinematic orange/teal. Top scene: modest Korean rented room, boarded cracked window, one rough table and simple tools left, kettle and bottle, old mattress and blanket right, a backpack, cold distant Seoul flats. Simplify the extravagant military workshop clutter into believable civilian scavenging. Off-center, weary, intimate documentary composition. It should look hand drawn by an indie game background artist, with uneven crosshatching and deliberate patches of unfinished paper, NOT a photograph with a pencil filter, NOT ornate AI concept art. Bottom icons must be stark pale chalk/graphite silhouettes on solid dark charcoal #171b1b backgrounds, rough expressive strokes and clear readable contours at 24px; no decorative busy texture behind icons. Maintain exact objects and grid: first row kettle+bottle, bedroll, backpack, open journal; second row map+compass, hammer+wrench, rolled bandage+medical tin, tin food+rice sack. No text, numbers, lettering, logos, watermarks, fancy frames or extra tiles. Keep every object within its tile with generous margins.
