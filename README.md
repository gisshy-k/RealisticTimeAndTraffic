# Realistic Time and Traffic (RTT)

A lightweight, performance-friendly mod for Cities: Skylines II that gives you full control over the flow of time and traffic volume in your city. It focuses strictly on time and traffic control, utilizing an improved and highly optimized implementation method. It allows for a unique playstyle: you can increase citizen trips to create a bustling, vibrant city atmosphere, while simultaneously slowing down the flow of time to smoothly manage the increased traffic.

Inspired by "Realistic Trips", this mod was created to ensure compatibility with mods like "MapExt" (or "EconomyEX"). 

**Note: All features are disabled by default upon subscribing to the mod. You must go to the options screen and check the features you want to use.**

## Features

### 🕒 Custom Time Flow
Adjust the flow of time (the clock and calendar) independently of the actual simulation processing speed.

*   **Days per Month**
    Increases the number of days in a month. The default value is 1, and it can be set between 1 and 30. For example, setting it to 2 means one in-game year will take 24 days.  
    ⚠️ **Warning:** Changing this value in an existing save file may shift the current date and significantly impact economic cycles and citizen aging.  
    ⚠️ **Note on Starting a New Game:** When you start a new game with a custom "Days Per Month" setting, you may notice that the starting month shifts to an earlier date. Please don't worry, **this is completely normal and harmless!** 

*   **Date Display Format**
    Choose from 3 different date display patterns. This setting becomes active when "Days per Month" is set to 2 or more.

*   **Slower Time Factor**
    Slows down the speed of the clock without delaying the simulation processing speed. Configurable between 1 and 10 in 0.5 increments. For example, setting it to 2 makes one day twice as long in game time.  
    **Note:** Since this only affects the visual clock speed, it is completely safe to change this value in an existing city at any time.

*   **Sync Aging & Demographics**
    Automatically adjusts citizen aging speed and birth rates to match your custom calendar, maintaining the vanilla game's demographic balance. These rates are dynamically scaled based strictly on your "Days Per Month" setting. For example, if "Days Per Month" is set to 3, citizens will age slower and the daily birth probability will operate at 1/3rd of the vanilla speed.  
    **Note:** The adjustments based on the "Slower Time Factor" introduced in v1.2.0 have been removed due to stability issues and the lack of necessity for demographic balancing.

### 🚗 Traffic Reduction Level
In vanilla C:S2, a strong, non-linear system automatically suppresses traffic (cancels trips) as your city grows larger. This mod allows you to lift or relax this suppression, bringing more citizens out into the streets. Conversely, you can also strengthen this suppression to restrict citizens from going out.

RTT introduces a custom mathematical algorithm ensuring that slider adjustments reflect linearly and intuitively on the actual traffic volume. It is specifically optimized for the play feel in mid-to-large cities (100k - 250k population) where traffic jams become a real challenge.

*   **15 (Ghost Town):** Suppresses traffic exponentially stronger than vanilla. Ideal for significantly lowering CPU load in massive cities.
*   **10 (Vanilla):** Standard game traffic volume.
*   **8 (Balanced):** Roughly twice the vanilla traffic.
*   **5 (Crowded):** Allows about ⅓ to ½ of the trips that the vanilla system would normally cancel.
*   **0 (Unleashed):** 100% of trips are generated with zero cancellations.

## Performance and Safety
Built purely on the ECS (Entity Component System). It does not add heavy per-frame calculations or inject complex logic into individual citizens (agents). Because it merely overrides global multipliers safely, the performance impact is negligible.

## Compatibility
*   Compatible with most mods.
*   May conflict with other mods that directly alter `TimeSettingsData` or `EconomyParameterData` (Traffic Reduction multipliers).

## Disclaimer
I am not a professional software engineer. This mod was built through dialogue with an AI assistant. The implementation methods have been carefully considered and thoroughly tested, and I am confident it runs stably, but I cannot guarantee perfect operation in all environments. Please use it at your own risk.


# Realistic Time and Traffic (RTT)

都市の時間の流れと交通量を完全にコントロールできるようになるMODです。市民の外出を増やして街の賑わいを演出しながら、時の流れを遅くすることで増えた交通量を捌いていくといった遊び方が可能です。

「Realistic Trips」に触発され、「MapExt（またはEconomyEX）」等のMODとの互換性を実現するために作成しました。時間と交通量のコントロールに機能を絞り、実装方法を改良した軽量でパフォーマンスに優れた設計になっています。

**注意: Modを購読しただけでは、全ての機能がオフになっていますので、必ずオプション画面で、利用したい機能にチェックを入れてください。**

## 機能

### 🕒 カスタムタイムフロー
シミュレーション自体の進行速度には影響を与えず、都市のカレンダーや時計の進み方だけを調整できます。

*   **月あたりの日数**
    1月あたりの日数を増やします。デフォルト値は「1」で、1～30日の間で設定可能です。例えば「2」に設定すると、ゲーム内の1年は24日になります。  
    ⚠️ **注意:** 既存の都市のセーブデータでこの値を変更すると、現在の日付がズレたり、経済サイクルや市民の年齢変化などに大きな影響を与える可能性があります。  
    ⚠️ **新規ゲーム開始時のカレンダー（月）のズレについて:** 「Days Per Month」の数値を変更して新規ゲームを開始すると、マップ本来の開始月からズレてスタートすることがあります。これはバグではなく、**安全な仕様ですのでご安心ください！**

*   **日付表示フォーマット**
    3つのパターンから選択できます。「月あたりの日数」を2日以上に設定した場合に有効になります。

*   **時間をゆっくりする係数**
    シミュレーションの処理速度を遅らせることなく、時計の進むスピードのみを遅くします。0.5刻みで「1～10」の間で設定可能です。例えば「2」に設定すると、1日の長さがゲーム時間で2倍になります。  
    **注:** こちらは時計の進むスピードのみを変更しているため、既存の都市でいつでも安全に変更可能です。

*   **加齢と人口動態の同期 (Sync Aging & Demographics)**
    1ヶ月あたりの人口の自然増減がバニラ（標準）と同等のバランスになるよう自動調整します。「月あたりの日数 (Days Per Month)」の設定値に合わせて、市民の加齢ペース、出生率が動的にスケーリングされます。例えば、「月あたりの日数」を3に設定した場合、加齢・出生の発生確率はバニラの1/3となります。  
    **注:** v1.2.0で導入された「Slower Time Factor（時間の進行を遅くする係数）」に基づく調整は、安定性の問題および人口バランス調整の必要性がないことから削除されました。

### 🚗 交通量削減水準 (Traffic Reduction Level)
バニラ（ゲーム本体）では、大都市になるほどシステム側から強力な「交通量抑制（外出キャンセル）」がかけられます。本MODではこの抑制を解除・緩和し、より多くの市民が街に繰り出すように設定できます。逆に、この抑制を強化し、市民の外出を制限することもできます。

RTTでは、交通量削減に関する独自の計算アルゴリズムを導入し、スライダーの操作が交通量に直線的かつ素直に反映されるようにしています。特に交通渋滞が本格化する中規模〜大規模都市（人口10万〜25万人）でのプレイフィールに最適化されています。

*   **15 (ゴーストタウン):** バニラの数十倍の強度で外出を抑制します。極限までCPU負荷を下げたい場合などに最適です。
*   **10 (バニラ):** ゲーム標準の交通量。
*   **8 (バランス):** バニラの約2倍程度の交通量になります。
*   **5 (混雑):** バニラがシステム的にキャンセルしていた外出の約⅓～½を許可します。
*   **0 (制限解除):** キャンセルなしで、市民の外出が100%発生します。

## パフォーマンスと安全性
純粋なECS（Entity Component System）上で構築されています。毎フレームごとの重い計算処理を追加したり、個々の市民（エージェント）に複雑なロジックを注入したりすることはありません。グローバルな乗数を安全に上書きするだけのアプローチをとっているため、パフォーマンスへの影響はごくわずかです。

## 互換性
*   ほとんどのMODと互換性があります。
*   `TimeSettingsData` または `EconomyParameterData`（交通量の削減係数）を直接書き換える他のMODとは競合する可能性があります。

## 免責事項
私は本職のエンジニアではありません。このMODはAIをアシスタントとした対話によって構築されました。実装方法は詳細に検討し、動作テストも行っており安定的に動くと確信していますが、動作を完全に保証するものではありません。自己責任でのご利用をお願いいたします。
