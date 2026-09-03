# NICO DRAW object art guide

> この文書をゲーム内オブジェクトの見た目に関する正本とする。新規追加・描き直しでは、単に画像を差し替えるのではなく、以下の視認性、物理判定、動的表現の分離方針を守る。

## 設計思想

### 1. 「子供の絵」だが、ゲームとして読めることを優先する

- 目標は、デジタル画像へ鉛筆フィルターを掛けた絵ではなく、ノートへ黒鉛筆と色鉛筆で描いたような絵。
- 輪郭は少し二重になり、線幅・筆圧・平行線がわずかに揺れる。ただし、何のオブジェクトか分からなくなるほど崩さない。
- 塗りはベタ塗りやグラデーションではなく、斜めの色鉛筆線を重ねる。紙色が少し残る密度にする。
- 小さく表示したときのシルエットを最優先する。細部を増やすより、「太い鍵」「大きい銃口」「開いた排出口」のような一つの特徴を強くする。
- 顔や文字を安易に足して説明しない。顔が必要なのは味方やボスなど、表情がゲーム上の役割を持つ場合だけ。
- 数値、残り時間、ゲージ、クールタイムなどの可変情報は画像へ焼き込まず、コード側の表示を重ねる。

### 2. 見た目とゲーム判定を分離する

- PNGスプライトは見た目だけを担当する。Collider、Rigidbody、Trigger、リンクID、ダメージ、価値などのゲーム仕様を画像サイズへ追従させない。
- 描き直しでスプライトの透明余白や縦横比が変わっても、既存の当たり判定を意図せず変えない。
- スプライトは `sprite.bounds.size` から表示したいワールドサイズを計算して配置する。PNGのピクセル寸法をゲーム上の大きさとして直接使わない。
- オブジェクト本体のTransformを表示調整だけのために縮小しない。Colliderまで縮む場合は、子GameObjectへスプライトを置いて子だけを調整する。
- 横に伸ばせる床や装置では一枚絵を無理に引き伸ばさない。トゲの反復、ベルトの等間隔マークなど、長さに応じて部品を繰り返す。

### 3. 動くものは「専用スプライト＋コード表現」にする

- ベルトコンベアの帯、製造機の排出、ボタンの沈み込み、発射装置のゲージなど、意味のある動きは残す。
- 本体の質感は専用PNG、動くゲージ・ランプ・数字・軌跡・発光はコード描画というハイブリッド構成を基本にする。
- 製造機は生成時に少し跳ねる、ボタンは押すと少し沈む、ランプは準備完了で色が変わるなど、操作結果を文字に頼らず伝える。
- 発光や警告は常時派手にしない。通常状態の絵を読めるようにし、必要な瞬間だけ色・光・動きを追加する。
- 動的な電線のように長さ・形・通電範囲が変わるものは固定PNG化しない。黒鉛筆の外線、色鉛筆の芯線、通電発光を重ねて描く。

### 4. 視認性と公平性を守る

- 見た目の外周と危険判定が大きくずれないようにする。ただし、装飾の突起や透明余白まで判定に含めない。
- 危険物、ボタン、ゴール、収集物は背景と明度・色相を分ける。
- コインの価値違いは数字を直接書かず、色、サイズ、縁、中央マークで判別させる。
- 操作対象は押下、発射、クールタイム復帰などの状態変化が見た目で分かるようにする。
- 固定カメラの特殊ステージは、全体表示だけを目的に引きすぎない。プレイヤーとギミックが読める最小倍率を選び、画面外から来る攻撃は予告表示で補う。

### 5. ステージ固有物も共通世界観へ揃える

- ステージ専用コントローラがコードで直接生成する傘、ボタン、電源、電球なども、共通オブジェクトと同じ品質基準を使う。
- 専用品だからといって単色の四角、円、線だけで済ませない。繰り返し登場する、画面上で大きい、攻略上重要、のいずれかに該当するものは専用スプライトを用意する。
- 一方で、予告線、レーザー、雨、電気、軌跡など形状が変化する演出はコード描画を使う。

## 画像生成・組み込み手順

1. 一つのPNGには一つのオブジェクトだけを描く。
2. 正面または真横の2Dゲーム用構図にし、外周へ十分な透明余白を設ける。
3. 背景、床、影、反射、透かし、不要な文字を含めない。
4. 生成時は単色クロマキー背景を使い、透過化後に四隅のAlphaと色残りを確認する。生成結果が最初から透過でも同じ確認を行う。
5. 最終PNGを `Assets/Resources/StageObjects/NicoDraw/` へ保存し、kebab-caseで命名する。
6. Unity Import Settingsは Sprite (2D and UI)、Alpha保持、MipMapなしを基本とする。
7. `Resources.Load<Sprite>` で読み込み、見つからない場合だけ従来のコード描画へフォールバックする。
8. 既存オブジェクトのID、Collider、Rigidbody、リンク、通信状態、保存JSONは変更しない。
9. 編集画面、通常プレイ、回転・拡縮、オンライン同期の各経路で同じ見た目になることを確認する。
10. 最後にUnity C#コンパイルと透過PNGの確認を行う。

## 画像生成用の共通プロンプト要旨

```text
NICO DRAWのゲーム内で使う2Dスプライト。
子供がノートへ黒鉛筆と色鉛筆で描いたような絵。
輪郭は少し二重で、線幅と形に小さな揺れがある。
塗りは斜めの色鉛筆線で、紙色の隙間を残す。
小さく表示しても一目で用途が分かる、太く単純なシルエット。
正面または真横。背景、影、反射、顔、文字、数字、透かしなし。
オブジェクトは中央に一つだけ置き、外周へ透明余白を取る。
```

個別プロンプトでは、材質、主要色、最も強調する一つの機能だけを追加する。装飾を増やしすぎない。

## Shared visual rules

- Draw every object as a readable 2D game sprite seen directly from the front or side.
- Aim for an actual child's careful notebook drawing made with graphite and colored pencils, not a polished digital illustration with a pencil texture applied afterward.
- Keep outlines visibly freehand: slightly doubled in places, wobbly, nonparallel and uneven in pressure, while retaining a readable silhouette.
- Use dark graphite, black pencil or a darker version of the fill color for outlines.
- Fill broad areas with visible diagonal colored-pencil hatching. Avoid flat digital fills, dense wax-like coverage and clean gradients; leave clear paper-colored gaps between strokes.
- Keep detail sparse enough that the object remains recognizable at its smallest in-game size.
- Use no background, cast shadow, contact shadow, reflection or watermark.
- Leave transparent padding around the silhouette so rotation and scaling do not clip the drawing.

## Current converted set

- Key: chunky classical key, graphite outline, yellow/gold pencil fill.
- Wood box: square wooden crate with the established X brace and sparse wood grain.
- Keyhole: classical keyhole silhouette rendered with layered charcoal strokes instead of a digital black fill.
- Goal: wide, old-fashioned wooden double exit door with aged planks, iron strap hinges and ring handles, replacing the UFO.
- Ink scale: chunky floor scale with a bent metal top plate, mustard/orange mechanical body, blank display and gauge slots; live numbers and gauge fill are overlaid by gameplay code.
- Triangle box: sturdy triangular wooden wedge with graphite framing and warm brown/orange diagonal pencil hatching.
- Spike: a single scarlet pencil-hatched spike repeated without stretching across the edited object width.
- Conveyor belt: a charcoal-blue moving belt with individually animated pencil treads and rotating mustard rollers; its animation follows the configured direction and speed.
- Box dropper: orange/mustard geared dispensing machine with a crate inspection window and a clearly open bottom chute.
- Spike dropper: red hazard-striped geared dispensing machine with a spike inspection window and a reinforced bottom chute.
- Bomb dropper: crooked orange pencil machine with a large bomb window and an unmistakable open bottom chute; the whole drawing kicks when it dispenses.
- Enemy dropper: loose purple pencil machine with a tangled enemy preview and a wide bottom chute; the whole drawing kicks when it dispenses.
- Beam emitter: simple cyan pencil box with one barrel, one status lamp and a large blank charge window; live charge fill and warning color are overlaid by gameplay code.
- Missile launcher: squat red pencil launcher with an oversized tube and one live ready lamp; the lamp changes from dark red to green as its cooldown completes.
- Dynamite: three uneven red pencil sticks with a bent fuse and oversized pale countdown badge; the live tenths-of-a-second number remains a code overlay.
- Friend spawner: sky-blue and mustard pencil machine with a simple friend preview window and large open chute; the body squashes when a friend is dispensed.
- Bazooka: oversized blue-gray pencil tube with a wide charcoal muzzle, brown rear cap and one simple grip; its collider and recoil remain independent from the artwork.
- Fish: friendly plump blue collectible fish with a navy pencil outline, visible cyan hatching, tail, fin and large eye.
- Handgun: compact blue-gray school-invention pistol with a brown pencil grip and a clearly readable muzzle and trigger.
- Jump pad: cyan top plate with a yellow direction arrow, twin graphite springs and a mustard base plate.
- Spike planet: purple cratered pencil core surrounded by evenly readable magenta-red spikes.
- Enemies deliberately avoid faces, eyes and mascot-like character design. Each is an asymmetric tangle of overshooting pencil loops whose ability is indicated only by a minimal silhouette cue.
- Walker enemy: shapeless purple knot with two crooked legs.
- Jumper enemy: orange scribble mass on two loose springs.
- Charger enemy: red racing knot with a blunt spike and trailing scratches.
- Flyer enemy: blue knot with mismatched leaf-like wings.
- Zigzag flyer: hot-pink knot with lightning-shaped wings and tail.
- Orbit flyer: teal scribble ball crossed by one crooked loop.
- Shooter enemy: moss-green knot with a rough cardboard-tube cannon.
- Bomber enemy: violet winged knot with a hanging charcoal bomb.
- Ghost enemy: pale violet-blue sheet silhouette with uneven wispy arms and a loose wavy hem; it has no face and uses sparse diagonal colored-pencil hatching so its wall-phasing shape stays readable.
- Arena boss: one tall purple bean scribble with bent antenna-like horns, stick arms and a blank face area for live expression lines.
- Flying boss: one rough violet circle with uneven spikes, two wing zigzags and three facial strokes.
- Chase boss: one wide purple oval with two loop arms, top spikes and three facial strokes.
- Mirror final boss keeps the players' drawings unchanged because visual identity with the real players is its core rule.
- Escort ally: one loose sky-blue circle scribble with two short legs and three facial marks.
- Defense ally: one blue bean scribble with stick limbs, three facial marks and a red heart scribble.
- Umbrella guide ally: one green circle and yellow raincoat triangle with stick legs and a raised arm; its separate gameplay umbrella uses the dedicated blue/cyan pencil sprite described below.
- Coin: thick, slightly crooked gold disc with graphite double rim, sparse yellow pencil hatching and a central sparkle mark. Runtime variants use tint and size instead of printed values.
- Rubber box: soft coral-red box with rounded crooked corners and a large spring zigzag; its bounce physics remain independent from the art.
- Grain emitter, ball, barrel, iron box, grain scale and grain gate use dedicated colored-pencil sprites while retaining their existing physics and weighing rules.
- Circuit power source: old yellow-orange electrical box with screws and a lightning mark.
- Circuit terminal: graphite and blue-gray contact fitting with a cyan inner ring. Terminal position remains code-controlled for moving gaps.
- Circuit bulb: old incandescent bulb with a visible filament and charcoal screw base. Tint, halo and filament light remain live code overlays.
- Circuit wire: not a fixed sprite; it uses graphite outer stroke, colored-pencil core and a powered glow so moving terminals and partial conduction remain accurate.
- Start marker: a chunky blue check-pattern flag with a graphite pole and grounded base. The spawn trigger remains separate.
- Stage 14-2 umbrella: a wide blue/cyan colored-pencil canopy with ribs, scalloped edge and full crooked handle. Rain shelter width remains controller-owned.
- Stage 14-3 shield button: pale metal pencil button that is tinted by its current link color, turns green and depresses briefly when activated.
- Stage 14-3 temporary steel shield: gray paper-steel fill, dark outline and colored diagonal pencil strokes; its one-second active state remains authoritative gameplay state.
- In-world monitors: one shared child-drawn blue television silhouette with crooked outlines, sparse pencil strokes, antennae and uneven feet. The pale paper screen is shared by challenge clocks and stage status panels so live text and gauges stay readable; avoid dark digital-device panels.
- Stage 6-3 chasing wall: pale red pencil-scribbled slab with crooked graphite edges and individually repeated side-facing colored-pencil spike sprites. Movement and the broad trigger remain controller-owned.
- Collectible pickup feedback: coins, fish and stars arc into the collecting character with a thin bright pencil trail, then produce two fast angular diamond flashes and a sparse burst of sharp directional slashes. Avoid soft expanding discs and large circular glows. Network snapshots hide already-collected items without replaying the effect.
- Stage 11-1 giant ghost: `enemy-ghost-giant.png` is the invulnerable chase silhouette. Its long faceless violet/blue pencil wisps distinguish it from the ordinary damageable `enemy-ghost.png`.
- Stage 11-1 lighting: `flashlight.png` is the mouse-aimed carryable light and `guide-lamp.png` is the fixed light used through the climb. Light masks and gameplay ranges remain code-controlled.

Runtime assets live under `Resources/StageObjects/NicoDraw`. Physics, stage IDs and saved data remain separate from the artwork.
