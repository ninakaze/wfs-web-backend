// Firebase Modular SDK v10
import { initializeApp } from "https://www.gstatic.com/firebasejs/10.12.2/firebase-app.js";
import { 
    getAuth, 
    GoogleAuthProvider,
    RecaptchaVerifier,
    signInWithPhoneNumber,
    signInWithPopup,
    createUserWithEmailAndPassword,
    signInWithEmailAndPassword,
    onAuthStateChanged,
    signOut
} from "https://www.gstatic.com/firebasejs/10.12.2/firebase-auth.js";
import { 
    getFirestore, 
    collection, 
    doc, 
    setDoc, 
    getDoc, 
    getDocs, 
    query, 
    orderBy 
} from "https://www.gstatic.com/firebasejs/10.12.2/firebase-firestore.js";

// Cấu hình Firebase: Tự động nạp động từ Backend API (/api/config/firebase) lấy theo file .env
// Không còn hard-code trong mã nguồn, team có thể chỉnh sửa mọi thông số qua file .env!
let dynamicConfig = null;

try {
    const res = await fetch("/api/config/firebase");
    if (res.ok) {
        const payload = await res.json();
        if (payload.success && payload.data) {
            dynamicConfig = payload.data;
            console.log("%c[Firebase Config] Đã nạp cấu hình động từ .env qua Backend API thành công!", "color: #10b981; font-weight: bold;");
        }
    }
} catch (err) {
    console.warn("[Firebase Config] Không thể nạp cấu hình động từ server:", err);
}

export const firebaseConfig = dynamicConfig || {
    apiKey: "",
    authDomain: "",
    projectId: "",
    storageBucket: "",
    messagingSenderId: "",
    appId: "",
    measurementId: ""
};

// Khởi tạo Firebase App với cấu hình động
export const app = initializeApp(firebaseConfig);
export const auth = getAuth(app);
export const db = getFirestore(app);

export {
    GoogleAuthProvider,
    RecaptchaVerifier,
    signInWithPhoneNumber,
    signInWithPopup,
    createUserWithEmailAndPassword,
    signInWithEmailAndPassword,
    onAuthStateChanged,
    signOut,
    collection,
    doc,
    setDoc,
    getDoc,
    getDocs,
    query,
    orderBy
};
