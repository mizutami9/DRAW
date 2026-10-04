import os
import sys
import sentencepiece as spm
import ctranslate2

sys.stdout.reconfigure(encoding='utf-8')

model_dir = r"D:\04_tiktok_game\NICODROW\models\nllb-200-distilled-600M-ct2-int8"

sp_model = os.path.join(model_dir, "sentencepiece.bpe.model")
sp = spm.SentencePieceProcessor()
sp.load(sp_model)

translator = ctranslate2.Translator(model_dir, device="cpu", compute_type="int8")

text = "Come here bro, jump on my head!"
tokens = sp.encode_as_pieces(text)
source_tokens = ["eng_Latn"] + tokens
target_prefix = [["jpn_Jpan"]]

results = translator.translate_batch([source_tokens], target_prefix=target_prefix)
target_tokens = results[0].hypotheses[0]
if target_tokens and target_tokens[0] == "jpn_Jpan":
    target_tokens = target_tokens[1:]

translated_text = sp.decode_pieces(target_tokens)
print("Original:", text)
print("Translated:", translated_text)
