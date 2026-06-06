document.addEventListener('DOMContentLoaded', function () {
    const chatToggle = document.getElementById('ai-chat-toggle');
    const chatHeader = document.querySelector('.ai-chat-header');
    const chatBody = document.getElementById('aiChatBody');
    const aiInput = document.getElementById('aiInput');
    const aiSend = document.getElementById('aiSend');
    const aiMessages = document.getElementById('aiMessages');

    function addMessage(text, who) {
        const div = document.createElement('div');
        div.className = 'ai-message ' + (who === 'user' ? 'user' : 'assistant');
        div.textContent = text;
        aiMessages.appendChild(div);
        aiMessages.scrollTop = aiMessages.scrollHeight;
    }

    function addRichMessage(text, who) {
        const div = document.createElement('div');
        div.className = 'ai-message ' + (who === 'user' ? 'user' : 'assistant');

        const lines = String(text || '').split(/\r?\n/);
        let hasImage = false;

        for (const line of lines) {
            const trimmed = line.trim();
            if (!trimmed) continue;

            if (trimmed.toLowerCase().startsWith('ảnh:')) {
                const imageUrl = trimmed.slice(4).trim();
                if (imageUrl) {
                    const img = document.createElement('img');
                    img.src = imageUrl;
                    img.alt = 'Ảnh sản phẩm';
                    img.loading = 'lazy';
                    img.className = 'ai-chat-product-image';
                    div.appendChild(img);
                    hasImage = true;
                }
                continue;
            }

            const p = document.createElement('div');
            p.textContent = line;
            div.appendChild(p);
        }

        if (!hasImage && div.childNodes.length === 0) {
            div.textContent = text;
        }

        aiMessages.appendChild(div);
        aiMessages.scrollTop = aiMessages.scrollHeight;
    }

    chatHeader.addEventListener('click', () => {
        chatBody.classList.toggle('d-none');
        chatToggle.textContent = chatBody.classList.contains('d-none') ? '–' : '×';
    });

    async function sendMessage() {
        const text = aiInput.value.trim();
        if (!text) return;
        addMessage(text, 'user');
        aiInput.value = '';
        addMessage('Đang gửi...', 'assistant');

        try {
            const res = await fetch('/api/ai/chat', {
                method: 'POST', headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ message: text })
            });
            const json = await res.json();
            // remove 'Đang gửi...'
            const last = aiMessages.lastChild;
            if (last && last.textContent === 'Đang gửi...') last.remove();
            addRichMessage(json.reply || 'Xin lỗi, không có phản hồi.', 'assistant');
        } catch (e) {
            const last = aiMessages.lastChild;
            if (last && last.textContent === 'Đang gửi...') last.remove();
            addMessage('Lỗi kết nối tới AI.', 'assistant');
        }
    }

    aiSend?.addEventListener('click', sendMessage);
    aiInput?.addEventListener('keydown', function (e) { if (e.key === 'Enter') sendMessage(); });
});
