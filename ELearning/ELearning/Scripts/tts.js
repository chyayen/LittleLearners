// Initialize SpeechSynthesis API
const synth = window.speechSynthesis;
let currentPage = -1;  // Start at -1 to account for the cover
const pages = document.querySelectorAll('.fb-page');  // Select all pages
const cover = document.querySelector('.fb-cover');  // Cover element
const playPauseBtn = document.getElementById('playPauseBtn');
const nextBtn = document.getElementById('nextBtn');
const prevBtn = document.getElementById('prevBtn');
let isPlaying = false;  // To track if reading is ongoing
let isCoverFlipped = false;
let currentWordIndex = 0;  // Track which word to start reading from 

// Function to display a specific page
const showPage = (pageIndex) => {
    cover.style.display = 'none';  // Hide cover by default
    //pages.forEach(page => page.style.display = 'none');  // Hide all pages
    pages.forEach((page, i) => {
        page.style.display = (i === pageIndex) ? 'block' : 'none'; // Show the current page
    });

    if (pageIndex === -1) {
        cover.style.display = 'flex';  // Show cover if index is -1
    } else {
        pages[pageIndex].style.display = 'block';  // Show the current page
    }
};

// Go to the specified page when the button is clicked
document.getElementById('goToPageBtn').addEventListener('click', () => {
    const pageNumber = parseInt(document.getElementById('pageInput').value) - 1; // Convert to zero-based index

    if (pageNumber >= 0 && pageNumber < pages.length) {
        currentPage = pageNumber;  // Update current page index
        showPage(currentPage); // Show the specified page
    } else {
        alert('Invalid page number!'); // Alert for invalid page number
    }
});

// Function to start TTS from the current page or clicked word
const startTTS = () => {
    if (!isPlaying) {
        isPlaying = true;
        if (currentPage === -1) {
            currentPage = 0;  // Start from the first page after the cover
            showPage(currentPage);
        }
        readPageContent(currentPage, currentWordIndex);  // Read from the current page
        togglePlayPauseButton(true);  // Update the button to show "Pause"
    }
};

// Function to stop TTS
const stopTTS = () => {
    synth.cancel();  // Stop any ongoing speech
    isPlaying = false;
    togglePlayPauseButton(false);  // Toggle the button back to "Play"
};

// Function to toggle Play/Pause button
const togglePlayPauseButton = (isPlaying) => {
    if (isPlaying) {
        playPauseBtn.innerHTML = '<i class="bi-stop-btn"></i>';
        playPauseBtn.classList.remove('btn-success');
        playPauseBtn.classList.add('btn-danger');
    } else {
        playPauseBtn.innerHTML = '<i class="bi-volume-up"></i>';
        playPauseBtn.classList.remove('btn-danger');
        playPauseBtn.classList.add('btn-success');
    }
};

// Function to read the content of the current page using TTS
const readPageContent = (pageIndex, wordIndex = 0) => {
    const pageContent = pages[pageIndex].querySelector('p').textContent;
    const words = pageContent.split(/\s+/);  // Split content into words
    const utterance = new SpeechSynthesisUtterance(words.slice(wordIndex).join(' '));  // Start from clicked word

    // Cancel any ongoing speech before starting new speech
    synth.cancel();
    synth.speak(utterance);  // Start the speech synthesis

    utterance.onend = () => {
        if (currentPage < pages.length - 1) {
            currentPage++;  // Move to the next page
            currentWordIndex = 0;  // Reset word index for the new page
            showPage(currentPage);
            readPageContent(currentPage);  // Auto-flip to the next page and read
        } else {
            stopTTS();  // Stop when the last page is reached
        }
    };

    //utterance.onerror = (e) => {
    //    console.error("TTS error occurred: ", e);
    //};
};

// Function to wrap each word in a span tag
const wrapWordsInSpans = (element) => {
    const words = element.textContent.trim().split(/\s+/);
    element.innerHTML = '';  // Clear the original content
    words.forEach((word, index) => {
        const span = document.createElement('span');
        span.textContent = word + ' ';
        span.setAttribute('data-index', index);  // Store the word index
        element.appendChild(span);
    });
};
//const wrapWordsInSpans = (element) => {
//    const walker = document.createTreeWalker(element, NodeFilter.SHOW_TEXT, {
//        acceptNode: (node) => {
//            // Only accept text nodes that have actual content (skip empty or whitespace-only nodes)
//            return node.nodeValue.trim() ? NodeFilter.FILTER_ACCEPT : NodeFilter.FILTER_REJECT;
//        }
//    });

//    let currentNode;
//    while (currentNode = walker.nextNode()) {
//        const words = currentNode.nodeValue.trim().split(/\s+/);
//        const fragment = document.createDocumentFragment();

//        words.forEach((word, index) => {
//            const span = document.createElement('span');
//            span.textContent = word + ' ';
//            span.setAttribute('data-index', index);  // Store the word index
//            fragment.appendChild(span); 
//        });

//        currentNode.parentNode.replaceChild(fragment, currentNode);  // Replace text node with spans 
//    }
//};

//// Apply it to all <p> elements with the class 'fb-page-content'
//document.querySelectorAll('p.fb-page-content').forEach(p => wrapWordsInSpans(p));


// Add event listener to start reading from clicked word
pages.forEach((page, pageIndex) => {
    const contentElement = page.querySelector('p');
    wrapWordsInSpans(contentElement);  // Wrap words in spans for interaction 

    contentElement.addEventListener('click', (event) => {
        if (event.target.tagName === 'SPAN') {
            currentWordIndex = parseInt(event.target.getAttribute('data-index'));  // Get word index
            currentPage = pageIndex;  // Set the current page to the clicked page
            stopTTS();  // Stop any ongoing TTS
            showPage(currentPage);  // Show the page
            readPageContent(currentPage, currentWordIndex);  // Read from the clicked word
            isPlaying = true;
            togglePlayPauseButton(true);  // Update button to show "Pause"
        }
    });
});

//pages.forEach((page, pageIndex) => {
//    page.querySelectorAll('p.fb-page-content').forEach((contentElement) => {
//        wrapWordsInSpans(contentElement); // Wrap words in spans

//        contentElement.addEventListener('click', (event) => {
//            if (event.target.tagName === 'SPAN') { // Check if the clicked element is a span
//                const currentWordIndex = parseInt(event.target.getAttribute('data-index'));  // Get word index
//                const currentPage = pageIndex;  // Set the current page to the clicked page

//                //stopTTS();  // Stop any ongoing TTS

//                // Optionally add a short delay before starting the next TTS
//                setTimeout(() => {
//                    // Remove the showPage function call if it's causing issues.
//                    // Instead, just call the readPageContent directly.
//                    readPageContent(currentPage, currentWordIndex);  // Read from the clicked word
//                    isPlaying = true; // Set playing status
//                    togglePlayPauseButton(true);  // Update button to show "Pause"
//                }, 100); // Delay of 100 milliseconds (optional)
//            }
//        });
//    });
//});




// Next button behavior for manual flipping
nextBtn.addEventListener('click', () => {
    if (currentPage < pages.length - 1) {
        currentPage++;
        currentWordIndex = 0;  // Reset word index
        showPage(currentPage);
        if (isPlaying) {
            stopTTS();  // Stop TTS if flipping manually
        }
    }
});

// Previous button behavior for manual flipping
prevBtn.addEventListener('click', () => {
    if (currentPage > -1) {
        currentPage--;
        currentWordIndex = 0;  // Reset word index
        showPage(currentPage);
        if (isPlaying) {
            stopTTS();  // Stop TTS if flipping manually
        }
    }
});

// Play/Pause button behavior
playPauseBtn.addEventListener('click', () => {
    if (!isPlaying) {
        startTTS();  // Start reading and auto-flipping
        togglePlayPauseButton(true);
    } else {
        stopTTS();  // Stop reading and auto-flipping
        togglePlayPauseButton(false);
    }
});

// Initialize by showing the cover on page load
document.addEventListener('DOMContentLoaded', () => {
    showPage(currentPage);  // Start with the cover (-1 index)
});