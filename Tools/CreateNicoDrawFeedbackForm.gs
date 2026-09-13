/**
 * Creates the public NICO DRAW Steam Playtest feedback form and a linked
 * response spreadsheet in the Google account that runs this script.
 *
 * Run once from https://script.google.com/ and copy the RESPONDER_URL printed
 * in the execution log into Assets/StreamingAssets/feedback_config.json.
 */
function createNicoDrawFeedbackForm() {
  const title = 'NICO DRAW Playtest Feedback / Bug Report';
  const form = FormApp.create(title, true);

  form
    .setDescription(
      'Thank you for playing NICO DRAW! Your feedback helps us improve the game.\n' +
      'NICO DRAWをプレイしていただきありがとうございます。今後の改善のため、ご意見・不具合報告をお寄せください。\n\n' +
      'Please do not include passwords or other sensitive information.\n' +
      'パスワードなどの機密情報は入力しないでください。'
    )
    .setConfirmationMessage(
      'Thanks for your feedback! / ご回答ありがとうございました！'
    )
    .setProgressBar(true)
    .setShuffleQuestions(false)
    .setShowLinkToRespondAgain(true)
    .setCollectEmail(false)
    .setLimitOneResponsePerUser(false);

  form.addMultipleChoiceItem()
    .setTitle('Language / 言語')
    .setChoiceValues(['English', '日本語', 'Other / その他'])
    .setRequired(true);

  form.addMultipleChoiceItem()
    .setTitle('How many players did you play with? / 何人でプレイしましたか？')
    .setChoiceValues(['1 Player', '2 Players', '3 Players', '4 Players'])
    .setRequired(true);

  form.addScaleItem()
    .setTitle('Overall experience / 総合評価')
    .setBounds(1, 5)
    .setLabels('1 - Poor / よくなかった', '5 - Excellent / とてもよかった')
    .setRequired(true);

  form.addMultipleChoiceItem()
    .setTitle('Did you encounter a bug? / バグに遭遇しましたか？')
    .setChoiceValues(['Yes / はい', 'No / いいえ', 'Not sure / わからない'])
    .setRequired(true);

  form.addListItem()
    .setTitle('Where did the bug occur? / バグが発生した場所・ステージ')
    .setChoiceValues([
      'Title / タイトル',
      'Lobby or Room / ロビー・ルーム',
      'Drawing or Preset / 書き直し・プリセット',
      '1-1',
      '1-2',
      '1-3',
      '6-3',
      '8-2',
      '9-3',
      '11-2',
      '14-3',
      'Other / その他',
      'No bug / バグなし'
    ])
    .setRequired(false);

  form.addParagraphTextItem()
    .setTitle('Bug details and reproduction steps / バグの内容・再現手順')
    .setHelpText(
      'What happened, what you expected, and what you were doing immediately before it happened. / ' +
      '起きたこと、期待した動作、直前に行っていた操作を書いてください。'
    )
    .setRequired(false);

  form.addTextItem()
    .setTitle('Most enjoyable stage / 一番楽しかったステージ')
    .setRequired(false);

  form.addTextItem()
    .setTitle('Confusing or less enjoyable stage / 分かりにくかった・面白くなかったステージ')
    .setRequired(false);

  form.addParagraphTextItem()
    .setTitle('Other feedback / その他のご意見')
    .setRequired(false);

  form.addTextItem()
    .setTitle('Steam name or contact (optional) / Steam名・連絡先（任意）')
    .setHelpText('Only enter this if you are comfortable being contacted. / 連絡を希望する場合のみ入力してください。')
    .setRequired(false);

  const responses = SpreadsheetApp.create(title + ' - Responses');
  form.setDestination(FormApp.DestinationType.SPREADSHEET, responses.getId());

  const responderUrl = form.getPublishedUrl();
  const editUrl = form.getEditUrl();

  console.log('RESPONDER_URL=' + responderUrl);
  console.log('EDIT_URL=' + editUrl);
  console.log('RESPONSES_SHEET_URL=' + responses.getUrl());

  return {
    responderUrl: responderUrl,
    editUrl: editUrl,
    responsesSheetUrl: responses.getUrl()
  };
}
