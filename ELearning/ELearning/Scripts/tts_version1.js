// Initialize the SpeechSynthesis API
const synth = window.speechSynthesis;
const playButton = document.getElementById('playButton');
const stopButton = document.getElementById('stopButton');

// Fetch the content element
const contentElement = document.getElementById('content');

// Function to split content into individual words
const splitIntoWords = (text) => text.trim().split(/\s+/);

// Function to highlight a clicked word
const highlightWord = (element) => {
    const highlightedElements = document.querySelectorAll('.highlight');
    highlightedElements.forEach(el => el.classList.remove('highlight'));
    element.classList.add('highlight');
};

// Convert the clicked word into speech and start reading from that point
const speakFromWord = (text, startIndex) => {
    const utterance = new SpeechSynthesisUtterance(text.slice(startIndex).join(' '));
    utterance.lang = 'en-US';

    synth.cancel(); // Stop any ongoing speech
    synth.speak(utterance);

    // Show stop button and hide play button
    toggleButtons(true);

    utterance.onend = () => {
        console.log("Speech synthesis finished.");
        // When speech ends, show play button and hide stop button
        toggleButtons(false);
    };

    console.log("Started reading from word: ", text[startIndex]);
};

// Add event listener to detect word clicks
contentElement.addEventListener('click', (event) => {
    const words = splitIntoWords(contentElement.textContent);

    if (event.target && event.target.nodeName === 'SPAN') {
        const wordIndex = Array.prototype.indexOf.call(contentElement.children, event.target);
        highlightWord(event.target);
        speakFromWord(words, wordIndex);
    }
});

// Dynamically wrap each word in a span tag
const wrapWordsInSpans = () => {
    const words = splitIntoWords(contentElement.textContent);
    contentElement.innerHTML = '';
    words.forEach(word => {
        const span = document.createElement('span');
        span.textContent = word + ' ';
        contentElement.appendChild(span);
    });

    console.log("Text split into words and wrapped in spans for interaction.");
};

// Function to toggle between play and stop buttons
const toggleButtons = (isPlaying) => {
    if (isPlaying) {
        playButton.style.display = 'none';
        stopButton.style.display = 'inline-block';
    } else {
        playButton.style.display = 'inline-block';
        stopButton.style.display = 'none';
    }
};

// Stop speech when the stop button is clicked
stopButton.addEventListener('click', () => {
    synth.cancel();
    toggleButtons(false);
    console.log("Speech synthesis stopped.");
});

// Play speech when the play button is clicked (starts from the beginning)
playButton.addEventListener('click', () => {
    const words = splitIntoWords(contentElement.textContent);
    highlightWord(contentElement.children[0]); // Highlight the first word
    speakFromWord(words, 0); // Start reading from the beginning
});

// Initialize by wrapping words when the page loads
document.addEventListener('DOMContentLoaded', wrapWordsInSpans);
