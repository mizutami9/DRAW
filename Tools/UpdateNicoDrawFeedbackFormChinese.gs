/**
 * Adds Simplified and Traditional Chinese to the existing NICO DRAW form.
 * Existing responses, responder URL, and response spreadsheet are preserved.
 */
function updateExistingNicoDrawFeedbackFormWithChinese() {
  const editUrl = 'https://docs.google.com/forms/d/1RGut67SetkcWloSJhyGTg2dGw69F4CQpy_KvSHAAkME/edit';
  const form = FormApp.openByUrl(editUrl);
  const items = form.getItems();

  if (items.length < 10) {
    throw new Error('Expected at least 10 form items, but found ' + items.length + '. No changes were made.');
  }

  form
    .setTitle('NICO DRAW Playtest Feedback / Bug Report / NICO DRAW 测试反馈 / 錯誤回報')
    .setDescription(
      'Thank you for playing NICO DRAW! Your feedback helps us improve the game.\n' +
      'NICO DRAWをプレイしていただきありがとうございます。ご意見・不具合報告をお寄せください。\n' +
      '感谢游玩 NICO DRAW！您的反馈将帮助我们改进游戏。\n' +
      '感謝遊玩 NICO DRAW！您的意見將幫助我們改進遊戲。\n\n' +
      'Please do not include passwords or other sensitive information.\n' +
      'パスワードなどの機密情報は入力しないでください。\n' +
      '请勿填写密码或其他敏感信息。 / 請勿填寫密碼或其他敏感資訊。'
    )
    .setConfirmationMessage(
      'Thanks for your feedback! / ご回答ありがとうございました！ / ' +
      '感谢您的反馈！ / 感謝您的意見！'
    );

  items[0].asMultipleChoiceItem()
    .setTitle('Language / 言語 / 语言 / 語言')
    .setChoiceValues(['English', '日本語', '简体中文', '繁體中文', 'Other / その他 / 其他'])
    .setRequired(true);

  items[1].asMultipleChoiceItem()
    .setTitle('How many players did you play with? / 何人でプレイしましたか？ / 游玩人数？ / 遊玩人數？')
    .setChoiceValues(['1 Player', '2 Players', '3 Players', '4 Players'])
    .setRequired(true);

  items[2].asScaleItem()
    .setTitle('Overall experience / 総合評価 / 总体评价 / 整體評價')
    .setBounds(1, 5)
    .setLabels('1 - Poor / よくなかった / 很差', '5 - Excellent / とてもよかった / 非常好')
    .setRequired(true);

  items[3].asMultipleChoiceItem()
    .setTitle('Did you encounter a bug? / バグに遭遇しましたか？ / 是否遇到错误？ / 是否遇到錯誤？')
    .setChoiceValues(['Yes / はい / 是', 'No / いいえ / 否', 'Not sure / わからない / 不确定 / 不確定'])
    .setRequired(true);

  items[4].asListItem()
    .setTitle('Where did the bug occur? / バグが発生した場所・ステージ / 错误发生位置 / 錯誤發生位置')
    .setChoiceValues([
      'Title / タイトル / 标题画面 / 標題畫面',
      'Lobby or Room / ロビー・ルーム / 大厅或房间 / 大廳或房間',
      'Drawing or Preset / 書き直し・プリセット / 重画或预设 / 重畫或預設',
      '1-1', '1-2', '1-3', '6-3', '8-2', '9-3', '11-2', '14-3',
      'Other / その他 / 其他',
      'No bug / バグなし / 未遇到错误 / 未遇到錯誤'
    ])
    .setRequired(false);

  items[5].asParagraphTextItem()
    .setTitle('Bug details and reproduction steps / バグの内容・再現手順 / 错误详情和复现步骤 / 錯誤詳情和重現步驟')
    .setHelpText(
      'What happened, what you expected, and what you were doing immediately before it happened. / ' +
      '起きたこと、期待した動作、直前の操作を書いてください。 / ' +
      '请描述发生的情况、预期结果以及发生前的操作。 / ' +
      '請描述發生的情況、預期結果以及發生前的操作。'
    )
    .setRequired(false);

  items[6].asTextItem()
    .setTitle('Most enjoyable stage / 一番楽しかったステージ / 最有趣的关卡 / 最有趣的關卡')
    .setRequired(false);

  items[7].asTextItem()
    .setTitle('Confusing or less enjoyable stage / 分かりにくかった・面白くなかったステージ / 难懂或不太有趣的关卡 / 難懂或不太有趣的關卡')
    .setRequired(false);

  items[8].asParagraphTextItem()
    .setTitle('Other feedback / その他のご意見 / 其他反馈 / 其他意見')
    .setRequired(false);

  items[9].asTextItem()
    .setTitle('Steam name or contact (optional) / Steam名・連絡先（任意） / Steam名称或联系方式（可选） / Steam名稱或聯絡方式（選填）')
    .setHelpText(
      'Only enter this if you are comfortable being contacted. / ' +
      '連絡を希望する場合のみ入力してください。 / ' +
      '仅在您愿意被联系时填写。 / 僅在您願意被聯絡時填寫。'
    )
    .setRequired(false);

  console.log('UPDATED_FORM=' + form.getPublishedUrl());
  console.log('EDIT_URL=' + form.getEditUrl());
}
