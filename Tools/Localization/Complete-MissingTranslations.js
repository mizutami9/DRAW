/* eslint-disable no-console */
const fs = require('fs');
const path = require('path');
const { execFileSync } = require('child_process');

const projectRoot = path.resolve(__dirname, '..', '..');
const localizationDirectory = path.join(projectRoot, 'Assets', 'Resources', 'Localization');
const localizationSourcePath = path.join(projectRoot, 'Assets', 'Scripts', 'LocalizationManager.cs');
const controllerLabels = /(?<![\p{L}\p{N}])(OPTIONS|CREATE|START|ENTER|BACKSPACE|ESC|TAB|L1|L2|R1|R2|VIEW|MENU)(?![\p{L}\p{N}])/giu;
const formatPlaceholder = /\{\d+(?:,[^}:]+)?(?::[^}]+)?\}/g;

function readJson(filePath) {
    return JSON.parse(fs.readFileSync(filePath, 'utf8'));
}

function getCanonicalEnglish() {
    const source = fs.readFileSync(localizationSourcePath, 'utf8');
    const table = new Map();
    const pairPattern = /\{\s*"([^"]+)"\s*,\s*"((?:\\.|[^"\\])*)"\s*\}/g;

    for (const dictionaryName of ['English', 'GeneratedEnglish']) {
        const declaration = `private static readonly Dictionary<string, string> ${dictionaryName}`;
        const start = source.indexOf(declaration);
        const end = source.indexOf('\n        };', start);
        if (start < 0 || end < 0) throw new Error(`Could not parse ${dictionaryName}.`);
        const block = source.slice(start, end);
        for (const match of block.matchAll(pairPattern)) {
            // JSON.parse provides the same escape handling needed by C# strings used here.
            table.set(match[1], JSON.parse(`"${match[2]}"`));
        }
    }

    for (const entry of readJson(path.join(localizationDirectory, 'en.json')).entries) {
        table.set(String(entry.key), String(entry.value));
    }
    return table;
}

function getCommittedLanguageFile(languageCode) {
    const gitPath = `Assets/Resources/Localization/${languageCode}.json`;
    const text = execFileSync('git', ['show', `HEAD:${gitPath}`], {
        cwd: projectRoot,
        encoding: 'utf8',
        maxBuffer: 10 * 1024 * 1024,
    });
    return JSON.parse(text);
}

function targetCode(languageCode) {
    if (languageCode === 'es-419') return 'es';
    if (languageCode === 'pt-BR' || languageCode === 'pt-PT') return 'pt';
    return languageCode;
}

function protectText(text) {
    const replacements = [];
    const protect = value => {
        const token = `{${9000 + replacements.length}}`;
        replacements.push({ token, value });
        return token;
    };

    let protectedText = text.replace(formatPlaceholder, protect);
    protectedText = protectedText.replace(controllerLabels, protect);
    protectedText = protectedText.replace(/\r?\n/g, () => protect('\n'));
    return { protectedText, replacements };
}

function restoreText(text, replacements) {
    let restored = text.trim();
    for (const { token, value } of replacements) {
        const number = token.slice(1, -1);
        const flexibleNumber = [...number].join('\\D*');
        restored = restored.replace(new RegExp(`\\{\\s*${flexibleNumber}\\s*\\}`, 'g'), () => value);
    }
    return restored;
}

function collectStrings(value, output) {
    if (typeof value === 'string') output.push(value);
    else if (Array.isArray(value)) for (const child of value) collectStrings(child, output);
}

async function translateBatch(texts, languageCode) {
    const url = new URL('https://translate.googleapis.com/translate_a/t');
    url.searchParams.set('client', 'dict-chrome-ex');
    url.searchParams.set('sl', 'en');
    url.searchParams.set('tl', targetCode(languageCode));
    for (const text of texts) url.searchParams.append('q', text);

    let lastError;
    for (let attempt = 1; attempt <= 4; attempt++) {
        try {
            const response = await fetch(url, {
                headers: {
                    'User-Agent': 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 Chrome/120 Safari/537.36',
                },
            });
            if (!response.ok) throw new Error(`HTTP ${response.status}: ${await response.text()}`);
            const raw = await response.json();
            const strings = [];
            collectStrings(raw, strings);
            const translated = strings.length === texts.length
                ? strings
                : strings.filter((_, index) => index % 2 === 0);
            if (translated.length !== texts.length) {
                throw new Error(`Expected ${texts.length} translations, received ${translated.length}.`);
            }
            if (translated.some((value, index) => texts[index].trim() && !String(value).trim())) {
                throw new Error('Translation service returned an empty item.');
            }
            return translated;
        } catch (error) {
            lastError = error;
            await new Promise(resolve => setTimeout(resolve, attempt * 800));
        }
    }
    throw lastError;
}

function placeholderSignature(text) {
    return [...text.matchAll(formatPlaceholder)].map(match => match[0]).sort().join('|');
}

function fixedTokensPreserved(text, replacements) {
    const requiredCounts = new Map();
    for (const { value } of replacements) requiredCounts.set(value, (requiredCounts.get(value) || 0) + 1);
    for (const [value, required] of requiredCounts) {
        if (!value) continue;
        let count = 0;
        let offset = 0;
        while ((offset = text.indexOf(value, offset)) >= 0) {
            count++;
            offset += value.length;
        }
        if (count < required) return false;
    }
    return true;
}

async function translatePreservingFixedSegments(text, languageCode) {
    const fixedPattern = /\{\d+(?:,[^}:]+)?(?::[^}]+)?\}|(?<![\p{L}\p{N}])(?:OPTIONS|CREATE|START|ENTER|BACKSPACE|ESC|TAB|L1|L2|R1|R2|VIEW|MENU)(?![\p{L}\p{N}])|\r?\n/giu;
    const parts = [];
    let lastIndex = 0;
    for (const match of text.matchAll(fixedPattern)) {
        if (match.index > lastIndex) parts.push({ fixed: false, value: text.slice(lastIndex, match.index) });
        parts.push({ fixed: true, value: match[0] });
        lastIndex = match.index + match[0].length;
    }
    if (lastIndex < text.length) parts.push({ fixed: false, value: text.slice(lastIndex) });

    const translatable = [];
    for (const part of parts) {
        if (part.fixed) continue;
        const match = part.value.match(/^(\s*)(.*?)(\s*)$/s);
        part.leading = match[1];
        part.core = match[2];
        part.trailing = match[3];
        if (part.core) {
            part.translationIndex = translatable.length;
            translatable.push(part.core);
        }
    }
    const translated = translatable.length ? await translateBatch(translatable, languageCode) : [];
    return parts.map(part => {
        if (part.fixed) return part.value;
        if (!part.core) return part.value;
        return `${part.leading}${translated[part.translationIndex]}${part.trailing}`;
    }).join('');
}

async function translateLanguage(languageCode, canonical) {
    // The committed file is the baseline so rerunning this tool never compounds a
    // partial or misaligned machine-translation result from an interrupted run.
    const json = getCommittedLanguageFile(languageCode);
    const entriesByKey = new Map(json.entries.map(entry => [String(entry.key), entry]));
    const todo = [...canonical.keys()]
        .filter(key => !entriesByKey.has(key)
            || !String(entriesByKey.get(key).value || '').trim()
            || placeholderSignature(canonical.get(key)) !== placeholderSignature(String(entriesByKey.get(key).value || '')))
        .sort();

    const prepared = todo.map(key => ({ key, ...protectText(canonical.get(key)) }));
    const batchSize = 20;
    for (let offset = 0; offset < prepared.length; offset += batchSize) {
        const batch = prepared.slice(offset, offset + batchSize);
        const translated = await translateBatch(batch.map(item => item.protectedText), languageCode);
        translated.forEach((value, index) => {
            const item = batch[index];
            item.translated = restoreText(value, item.replacements);
        });

        for (const item of batch) {
            if (!item.translated.trim()
                || !fixedTokensPreserved(item.translated, item.replacements)
                || placeholderSignature(canonical.get(item.key)) !== placeholderSignature(item.translated)) {
                item.translated = await translatePreservingFixedSegments(canonical.get(item.key), languageCode);
            }
        }
        await new Promise(resolve => setTimeout(resolve, 180));
    }

    for (const item of prepared) {
        if (!item.translated || !item.translated.trim()) throw new Error(`[${languageCode}] Empty translation: ${item.key}`);
        if (placeholderSignature(canonical.get(item.key)) !== placeholderSignature(item.translated)) {
            throw new Error(`[${languageCode}] Placeholder mismatch: ${item.key}\nEN: ${canonical.get(item.key)}\nTR: ${item.translated}`);
        }
        if (entriesByKey.has(item.key)) entriesByKey.get(item.key).value = item.translated;
        else {
            const entry = { key: item.key, value: item.translated };
            json.entries.push(entry);
            entriesByKey.set(item.key, entry);
        }
    }

    fs.writeFileSync(path.join(localizationDirectory, `${languageCode}.json`), `${JSON.stringify(json, null, 2)}\n`, 'utf8');
    console.log(`[${languageCode}] saved ${todo.length} translations (${json.entries.length} entries).`);
}

async function main() {
    const canonical = getCanonicalEnglish();
    const manifest = readJson(path.join(localizationDirectory, 'languages.json'));
    const requested = process.argv.slice(2);
    const available = manifest.entries.map(entry => String(entry.code)).filter(code => code !== 'ja' && code !== 'en');
    const languages = requested.length ? requested : available;
    for (const languageCode of languages) {
        if (!available.includes(languageCode)) throw new Error(`Unknown language: ${languageCode}`);
        await translateLanguage(languageCode, canonical);
    }
}

main().catch(error => {
    console.error(error.stack || error.message || error);
    process.exitCode = 1;
});
